using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace FFmpegRunner
{
    /// <summary>
    /// FFmpeg 运行器核心类，封装 FFmpeg 进程的启动、监控和终止逻辑，
    /// 支持同步和异步两种运行模式。
    /// </summary>
    public class FFmpegRunner : IDisposable
    {
        private Process? _process;
        private IPipeInterface? _pipe;
        private bool _disposed;
        private string? _ffmpegPath;

        /// <summary>
        /// 获取当前正在运行的 <see cref="System.Diagnostics.Process"/> 实例。
        /// 在调用 <see cref="Start"/> 或 <see cref="StartAsync"/> 之后可用。
        /// </summary>
        public Process? Process => _process;

        /// <summary>
        /// 获取 FFmpeg 可执行文件的完整路径。如果构造时未指定，则返回 <see cref="FFmpegConfig.FFmpegPath"/> 的值。
        /// 不保证路径有效性，实际运行时会进行验证。
        /// </summary>
        public string FFmpegPath => _ffmpegPath ?? FFmpegConfig.FFmpegPath ?? "ffmpeg";

        /// <summary>
        /// 获取媒体源文件路径或流地址。
        /// </summary>
        public string SourcePath { get; }

        /// <summary>
        /// 获取应用于输入源之前的 FFmpeg 输入参数（如 -r、-buffer_size、-timeout 等）。
        /// 这些参数会被放置在 <c>-i</c> 之前，仅对下一个输入生效。
        /// </summary>
        public string InputArguments { get; internal set; } = string.Empty;

        /// <summary>
        /// 获取完整的 FFmpeg 命令行参数（输出参数部分，放置在输出目标之前）。
        /// </summary>
        public string CommandArguments { get; }

        /// <summary>
        /// 获取输出目标地址（文件路径、管道路径或网络 URL）。
        /// </summary>
        public string TargetPath { get; }

        /// <summary>
        /// 获取标准输出流的完整内容。
        /// </summary>
        public string StandardOutput { get; private set; } = string.Empty;

        /// <summary>
        /// 获取标准错误流的完整内容（包含 FFmpeg 的日志和进度信息）。
        /// </summary>
        public string StandardError { get; private set; } = string.Empty;

        /// <summary>
        /// 获取或设置是否使用 -y 标志覆盖输出文件。默认值为 <c>true</c>。
        /// </summary>
        public bool Overwrite { get; internal set; } = true;

        /// <summary>
        /// 获取或设置进程超时时间（毫秒）。默认值 0 表示无超时。
        /// </summary>
        public int TimeoutMilliseconds { get; set; }

        /// <summary>
        /// 获取当前使用的管道接口实例。当输出目标为 pipe 模式时可用。
        /// </summary>
        public IPipeInterface? Pipe => _pipe;

        /// <summary>
        /// 初始化 <see cref="FFmpegRunner"/> 类的新实例。
        /// </summary>
        /// <param name="ffmpegPath">FFmpeg 可执行文件路径。</param>
        /// <param name="sourcePath">媒体源文件路径或流地址。</param>
        /// <param name="commandArguments">FFmpeg 命令行参数。</param>
        /// <param name="targetPath">输出目标地址。</param>
        /// <param name="pipe">管道接口实例。为 <c>null</c> 时不使用管道输出。</param>
        public FFmpegRunner(
            string? ffmpegPath,
            string sourcePath,
            string commandArguments,
            string targetPath,
            IPipeInterface? pipe = null)
        {
            SourcePath = sourcePath ?? throw new ArgumentNullException(nameof(sourcePath));
            CommandArguments = commandArguments ?? throw new ArgumentNullException(nameof(commandArguments));
            TargetPath = targetPath ?? throw new ArgumentNullException(nameof(targetPath));
            _ffmpegPath = ffmpegPath;
            _pipe = pipe;
        }

        /// <summary>
        /// 查询源地址的媒体流信息（分辨率、编码格式、比特率等）。
        /// 内部调用 ffprobe 获取 JSON 格式的媒体信息并解析。
        /// </summary>
        /// <returns>包含所有媒体流信息的列表。</returns>
        public List<StreamInfo> GetStreamInfo()
        {
            var ffprobePath = GetFFprobePath();
            var arguments = $"-v quiet -print_format json -show_format -show_streams \"{SourcePath}\"";

            using var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = ffprobePath,
                    Arguments = arguments,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true,
                    StandardOutputEncoding = Encoding.UTF8,
                }
            };

            process.Start();

            var output = process.StandardOutput.ReadToEnd();
            var error = process.StandardError.ReadToEnd();

            process.WaitForExit();

            if (process.ExitCode != 0)
            {
                throw new InvalidOperationException(
                    $"ffprobe 执行失败 (ExitCode={process.ExitCode}): {error}");
            }

            return ParseStreamInfo(output);
        }

        /// <summary>
        /// 同步启动 FFmpeg 进程。进程启动后将阻塞当前线程直到进程退出。
        /// </summary>
        /// <returns>进程退出代码。</returns>
        public int Start()
        {
            ThrowIfAlreadyRunning();

            _pipe?.Initialize();

            _process = CreateProcess();

            try
            {
                _pipe?.Start(CancellationToken.None);

                _process.Start();

                var stdErrTask = Task.Run(() => _process.StandardError.ReadToEnd());
                var stdOutTask = Task.Run(() => _process.StandardOutput.ReadToEnd());

                var exited = _process.WaitForExit(TimeoutMilliseconds > 0
                    ? TimeoutMilliseconds
                    : int.MaxValue);

                if (!exited)
                {
                    KillProcess();
                    throw new TimeoutException(
                        $"FFmpeg 进程执行超时 ({TimeoutMilliseconds}ms)。");
                }

                StandardError = stdErrTask.Result;
                StandardOutput = stdOutTask.Result;

                _pipe?.Stop();

                return _process.ExitCode;
            }
            catch (Exception) when (_process?.HasExited == false)
            {
                KillProcess();
                throw;
            }
        }

        /// <summary>
        /// 异步启动 FFmpeg 进程，支持通过 <see cref="CancellationToken"/> 取消操作。
        /// </summary>
        /// <param name="cancellationToken">用于取消进程执行的令牌。</param>
        /// <returns>进程退出代码。</returns>
        public async Task<int> StartAsync(CancellationToken cancellationToken = default)
        {
            ThrowIfAlreadyRunning();

            _pipe?.Initialize();

            _process = CreateProcess();

            try
            {
                _pipe?.Start(cancellationToken);

                _process.Start();

                var stdErrTask = _process.StandardError.ReadToEndAsync();
                var stdOutTask = _process.StandardOutput.ReadToEndAsync();

                using var timeoutCts = new CancellationTokenSource();

                if (TimeoutMilliseconds > 0)
                {
                    timeoutCts.CancelAfter(TimeoutMilliseconds);
                }

                using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(
                    cancellationToken, timeoutCts.Token);

                var tcs = new TaskCompletionSource<bool>();

                _process.EnableRaisingEvents = true;
                _process.Exited += (_, _) => tcs.TrySetResult(true);

                linkedCts.Token.Register(() =>
                {
                    try
                    {
                        if (!_process.HasExited)
                        {
                            _process.Kill();
                        }
                    }
                    catch (InvalidOperationException)
                    {
                    }

                    tcs.TrySetCanceled(linkedCts.Token);
                });

                try
                {
                    await tcs.Task.ConfigureAwait(false);
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    StandardError = await stdErrTask.ConfigureAwait(false);
                    StandardOutput = await stdOutTask.ConfigureAwait(false);
                    _pipe?.Stop();
                    cancellationToken.ThrowIfCancellationRequested();
                }

                StandardError = await stdErrTask.ConfigureAwait(false);
                StandardOutput = await stdOutTask.ConfigureAwait(false);

                _pipe?.Stop();

                return _process.ExitCode;
            }
            catch (Exception) when (_process?.HasExited == false)
            {
                KillProcess();
                throw;
            }
        }

        /// <summary>
        /// 终止当前正在运行的 FFmpeg 进程。
        /// </summary>
        public void Stop()
        {
            KillProcess();
        }

        /// <summary>
        /// 释放进程和管道资源。
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// 释放托管和非托管资源。
        /// </summary>
        protected virtual void Dispose(bool disposing)
        {
            if (_disposed)
                return;

            if (disposing)
            {
                _pipe?.Stop();
                KillProcess();
                _pipe?.Dispose();
                _process?.Dispose();
            }

            _disposed = true;
        }

        private Process CreateProcess()
        {
            var resolvedPath = ResolveFFmpegPath();
            var outputTarget = GetOutputTarget();
            var overwriteFlag = Overwrite ? "-y " : string.Empty;

            var startInfo = new ProcessStartInfo
            {
                FileName = resolvedPath,
                Arguments = $"{InputArguments} {overwriteFlag}-i \"{SourcePath}\" {CommandArguments} {outputTarget}",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
                StandardOutputEncoding = Encoding.UTF8,
                StandardErrorEncoding = Encoding.UTF8,
            };

            return new Process { StartInfo = startInfo };
        }

        private string GetOutputTarget()
        {
            if (_pipe != null)
                return _pipe.GetOutputTarget();

            return TargetPath;
        }

        private string ResolveFFmpegPath()
        {
            var path = FFmpegPath;

            if (File.Exists(path))
                return path;

            var resolved = FFmpegConfig.GetFFmpegPath();
            _ffmpegPath = resolved;
            return resolved;
        }

        private void KillProcess()
        {
            if (_process == null)
                return;

            try
            {
                if (!_process.HasExited)
                {
                    _process.Kill();
                    _process.WaitForExit(5000);
                }
            }
            catch (InvalidOperationException)
            {
            }
        }

        private void ThrowIfAlreadyRunning()
        {
            if (_process != null && !_process.HasExited)
            {
                throw new InvalidOperationException("FFmpeg 进程已在运行中。");
            }
        }

        private string GetFFprobePath()
        {
            var ffmpegPath = ResolveFFmpegPath();
            var directory = Path.GetDirectoryName(ffmpegPath) ?? string.Empty;
            var ffprobeName = ffmpegPath.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)
                ? "ffprobe.exe"
                : "ffprobe";

            var ffprobePath = Path.Combine(directory, ffprobeName);

            if (!File.Exists(ffprobePath))
            {
                ffprobePath = Path.Combine(directory, "ffprobe");
                if (!File.Exists(ffprobePath))
                {
                    throw new FileNotFoundException(
                        $"无法找到 ffprobe 可执行文件。期望路径: {Path.Combine(directory, ffprobeName)}");
                }
            }

            return ffprobePath;
        }

        private static List<StreamInfo> ParseStreamInfo(string json)
        {
            var result = new List<StreamInfo>();

            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            if (root.TryGetProperty("streams", out var streams))
            {
                foreach (var stream in streams.EnumerateArray())
                {
                    var info = new StreamInfo
                    {
                        Index = stream.TryGetProperty("index", out var idx) ? idx.GetInt32() : 0,
                        CodecType = stream.TryGetProperty("codec_type", out var ct) ? ct.GetString() ?? string.Empty : string.Empty,
                        CodecName = stream.TryGetProperty("codec_name", out var cn) ? cn.GetString() ?? string.Empty : string.Empty,
                        Width = stream.TryGetProperty("width", out var w) ? w.GetInt32() : (int?)null,
                        Height = stream.TryGetProperty("height", out var h) ? h.GetInt32() : (int?)null,
                        PixFmt = stream.TryGetProperty("pix_fmt", out var pf) ? pf.GetString() : null,
                        RFrameRate = stream.TryGetProperty("r_frame_rate", out var rfr) ? rfr.GetString() : null,
                        BitRate = stream.TryGetProperty("bit_rate", out var br) && br.TryGetInt64(out var brv) ? brv : (long?)null,
                        SampleRate = stream.TryGetProperty("sample_rate", out var sr) && sr.TryGetInt32(out var srv) ? srv : (int?)null,
                        Channels = stream.TryGetProperty("channels", out var ch) && ch.TryGetInt32(out var chv) ? chv : (int?)null,
                        SampleFmt = stream.TryGetProperty("sample_fmt", out var sf) ? sf.GetString() : null,
                        Duration = stream.TryGetProperty("duration", out var dur) && dur.TryGetDouble(out var durv) ? durv : (double?)null,
                    };

                    if (stream.TryGetProperty("tags", out var tags))
                    {
                        foreach (var tag in tags.EnumerateObject())
                        {
                            info.Tags[tag.Name] = tag.Value.GetString() ?? string.Empty;
                        }
                    }

                    result.Add(info);
                }
            }

            if (root.TryGetProperty("format", out var format))
            {
                if (format.TryGetProperty("duration", out var dur) && dur.TryGetDouble(out var durv))
                {
                    foreach (var info in result)
                    {
                        if (info.Duration == null)
                        {
                            info.Duration = durv;
                        }
                    }
                }

                if (format.TryGetProperty("bit_rate", out var br) && br.TryGetInt64(out var brv))
                {
                    foreach (var info in result)
                    {
                        if (info.BitRate == null)
                        {
                            info.BitRate = brv;
                        }
                    }
                }
            }

            foreach (var info in result)
            {
                info.RawJson = json;
            }

            return result;
        }
    }
}