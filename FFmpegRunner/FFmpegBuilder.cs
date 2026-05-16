using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace FFmpegRunner
{
    /// <summary>
    /// FFmpeg 参数构建器，提供 Fluent API 风格的链式调用接口，
    /// 用于构建 FFmpeg 命令行参数并生成 <see cref="FFmpegRunner"/> 实例。
    /// </summary>
    public class FFmpegBuilder
    {
        private static readonly HashSet<string> ValidRtspInputTransports = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "tcp", "udp", "http"
        };

        private static readonly HashSet<string> ValidRtspOutputTransports = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "tcp", "udp"
        };

        private readonly StringBuilder _arguments = new StringBuilder();
        private InputOptions? _inputOptions;
        private string _sourcePath = string.Empty;
        private string? _ffmpegPath;
        private string _targetPath = string.Empty;
        private Action<byte[], FrameMetadata?>? _pipeDataCallback;
        private bool _overwrite;
        private bool _usePipeOutput;
        private string _pipeName = Guid.NewGuid().ToString("N");
        private int _bufferCapacity = 100;
        private PipeType _pipeType = PipeType.Stream;
        private IFrameAnalyzer? _frameAnalyzer;
        private bool _videoCodecExplicitlySet;
        private bool _isRtspOutput;

        /// <summary>
        /// 初始化 <see cref="FFmpegBuilder"/> 类的新实例。
        /// </summary>
        public FFmpegBuilder()
        {
        }

        /// <summary>
        /// 设置 FFmpeg 可执行文件路径。
        /// </summary>
        /// <param name="path">FFmpeg 可执行文件的完整路径。为 <c>null</c> 时使用自动探测。</param>
        /// <returns>当前构建器实例。</returns>
        public FFmpegBuilder WithFFmpegPath(string? path)
        {
            _ffmpegPath = path;
            return this;
        }

        /// <summary>
        /// 设置媒体源文件路径或流地址。
        /// </summary>
        /// <param name="sourcePath">媒体源路径。</param>
        /// <param name="configureInput">
        /// 可选输入参数配置回调，用于设置位于 <c>-i</c> 之前的输入选项。
        /// 如 <c>opt => opt.WithFrameRate(30).WithHardwareAcceleration("cuda")</c>。
        /// </param>
        /// <returns>当前构建器实例。</returns>
        public FFmpegBuilder FromSource(string sourcePath, Action<InputOptions>? configureInput = null)
        {
            _sourcePath = sourcePath ?? throw new ArgumentNullException(nameof(sourcePath));

            if (configureInput != null)
            {
                _inputOptions = new InputOptions();
                configureInput(_inputOptions);
            }

            return this;
        }

        /// <summary>
        /// 配置 RTSP 拉流源地址，支持通过回调配置输入参数。
        /// </summary>
        /// <param name="rtspAddress">RTSP 拉流地址，如 <c>rtsp://192.168.1.100:554/stream</c>。</param>
        /// <param name="transportProtocol">RTSP 传输协议，支持 "tcp"（默认）、"udp"、"http"。</param>
        /// <param name="configureInput">
        /// 可选输入参数配置回调。RTSP 传输协议会自动添加，回调中可设置额外的输入参数。
        /// 如 <c>opt => opt.WithBufferSize(65536).WithTimeout(5000000)</c>。
        /// </param>
        /// <returns>当前构建器实例。</returns>
        /// <exception cref="ArgumentNullException">当 <paramref name="rtspAddress"/> 为 <c>null</c> 或空时抛出。</exception>
        /// <exception cref="ArgumentException">当 <paramref name="transportProtocol"/> 不在允许的值范围内时抛出。</exception>
        public FFmpegBuilder FromRtspSource(string rtspAddress, string transportProtocol = "tcp", Action<InputOptions>? configureInput = null)
        {
            if (string.IsNullOrWhiteSpace(rtspAddress))
                throw new ArgumentNullException(nameof(rtspAddress));

            if (!ValidRtspInputTransports.Contains(transportProtocol))
                throw new ArgumentException(
                    $"无效的 RTSP 传输协议: {transportProtocol}。允许的值: {string.Join(", ", ValidRtspInputTransports)}",
                    nameof(transportProtocol));

            _sourcePath = rtspAddress;

            _inputOptions = new InputOptions();
            _inputOptions.WithRtspTransport(transportProtocol);
            configureInput?.Invoke(_inputOptions);

            return this;
        }

        /// <summary>
        /// 设置输出文件覆盖选项（-y）。
        /// </summary>
        /// <param name="overwrite">是否覆盖已有文件。</param>
        /// <returns>当前构建器实例。</returns>
        public FFmpegBuilder WithOverwrite(bool overwrite = true)
        {
            _overwrite = overwrite;
            return this;
        }

        /// <summary>
        /// 设置网络协议超时（单位：微秒）。
        /// </summary>
        /// <param name="timeoutMicroseconds">超时时间（微秒）。</param>
        /// <returns>当前构建器实例。</returns>
        public FFmpegBuilder WithNetworkTimeout(long timeoutMicroseconds)
        {
            _arguments.Append($" -timeout {timeoutMicroseconds}");
            return this;
        }

        /// <summary>
        /// 设置网络缓冲区大小（单位：字节）。
        /// </summary>
        /// <param name="bufferSize">缓冲区大小（字节）。</param>
        /// <returns>当前构建器实例。</returns>
        public FFmpegBuilder WithBufferSize(int bufferSize)
        {
            _arguments.Append($" -buffer_size {bufferSize}");
            return this;
        }

        /// <summary>
        /// 设置 RTSP 传输协议。
        /// </summary>
        /// <param name="transport">传输协议（tcp 或 udp）。</param>
        /// <returns>当前构建器实例。</returns>
        public FFmpegBuilder WithRtspTransport(string transport)
        {
            _arguments.Append($" -rtsp_transport {transport}");
            return this;
        }

        /// <summary>
        /// 设置视频编码器。
        /// </summary>
        /// <param name="codec">编码器名称（如 h264、h265、mpeg4）。</param>
        /// <returns>当前构建器实例。</returns>
        public FFmpegBuilder WithVideoCodec(string codec)
        {
            _arguments.Append($" -c:v {codec}");
            _videoCodecExplicitlySet = true;
            return this;
        }

        /// <summary>
        /// 清除已设置的视频编码器配置（-c:v），恢复为默认行为。
        /// 调用此方法后，系统将使用默认的 H.264 输出编码器。
        /// </summary>
        /// <returns>当前构建器实例。</returns>
        public FFmpegBuilder WithoutVideoCodec()
        {
            var args = _arguments.ToString();
            var pattern = @"-c:v\s+\S+";
            var result = Regex.Replace(args, pattern, "");
            result = Regex.Replace(result, @"\s+", " ").Trim();

            _arguments.Clear();
            _arguments.Append(result);
            _videoCodecExplicitlySet = false;

            return this;
        }

        /// <summary>
        /// 设置音频编码器。
        /// </summary>
        /// <param name="codec">编码器名称（如 aac、mp3、opus）。</param>
        /// <returns>当前构建器实例。</returns>
        public FFmpegBuilder WithAudioCodec(string codec)
        {
            _arguments.Append($" -c:a {codec}");
            return this;
        }

        /// <summary>
        /// 设置视频比特率。
        /// </summary>
        /// <param name="bitrate">比特率字符串（如 "2M"、"1500k"）。</param>
        /// <returns>当前构建器实例。</returns>
        public FFmpegBuilder WithVideoBitrate(string bitrate)
        {
            _arguments.Append($" -b:v {bitrate}");
            return this;
        }

        /// <summary>
        /// 设置音频比特率。
        /// </summary>
        /// <param name="bitrate">比特率字符串（如 "128k"、"192k"）。</param>
        /// <returns>当前构建器实例。</returns>
        public FFmpegBuilder WithAudioBitrate(string bitrate)
        {
            _arguments.Append($" -b:a {bitrate}");
            return this;
        }

        /// <summary>
        /// 设置视频 CRF 质量控制参数。
        /// </summary>
        /// <param name="crf">CRF 值（0-51，越低质量越好）。</param>
        /// <returns>当前构建器实例。</returns>
        public FFmpegBuilder WithCrf(int crf)
        {
            _arguments.Append($" -crf {crf}");
            return this;
        }

        /// <summary>
        /// 设置视频分辨率。
        /// </summary>
        /// <param name="resolution">分辨率字符串（如 "1920x1080"、"1280x720"）。</param>
        /// <returns>当前构建器实例。</returns>
        public FFmpegBuilder WithResolution(string resolution)
        {
            _arguments.Append($" -s {resolution}");
            return this;
        }

        /// <summary>
        /// 设置视频帧率。
        /// </summary>
        /// <param name="frameRate">帧率值。</param>
        /// <returns>当前构建器实例。</returns>
        public FFmpegBuilder WithFrameRate(int frameRate)
        {
            _arguments.Append($" -r {frameRate}");
            return this;
        }

        /// <summary>
        /// 设置音频采样率。
        /// </summary>
        /// <param name="sampleRate">采样率（如 44100、48000）。</param>
        /// <returns>当前构建器实例。</returns>
        public FFmpegBuilder WithAudioSampleRate(int sampleRate)
        {
            _arguments.Append($" -ar {sampleRate}");
            return this;
        }

        /// <summary>
        /// 设置音频声道数。
        /// </summary>
        /// <param name="channels">声道数（1=单声道，2=立体声）。</param>
        /// <returns>当前构建器实例。</returns>
        public FFmpegBuilder WithAudioChannels(int channels)
        {
            _arguments.Append($" -ac {channels}");
            return this;
        }

        /// <summary>
        /// 设置输出格式（容器）。
        /// </summary>
        /// <param name="format">格式名称（如 mp4、avi、flv、matroska）。</param>
        /// <returns>当前构建器实例。</returns>
        /// <remarks>
        /// 注意：当调用 <see cref="ToRtsp"/> 配置 RTSP 推流后，此方法设置的格式参数将被 RTSP 推流格式（-f rtsp）覆盖并忽略。
        /// </remarks>
        public FFmpegBuilder WithFormat(string format)
        {
            _arguments.Append($" -f {format}");
            return this;
        }

        /// <summary>
        /// 设置像素格式。
        /// </summary>
        /// <param name="pixFmt">像素格式（如 yuv420p、rgb24）。</param>
        /// <returns>当前构建器实例。</returns>
        public FFmpegBuilder WithPixelFormat(string pixFmt)
        {
            _arguments.Append($" -pix_fmt {pixFmt}");
            return this;
        }

        /// <summary>
        /// 设置视频预设速度。
        /// </summary>
        /// <param name="preset">预设名称（如 ultrafast、fast、medium、slow）。</param>
        /// <returns>当前构建器实例。</returns>
        public FFmpegBuilder WithPreset(string preset)
        {
            _arguments.Append($" -preset {preset}");
            return this;
        }

        /// <summary>
        /// 设置线程数。
        /// </summary>
        /// <param name="threadCount">线程数。0 表示自动。</param>
        /// <returns>当前构建器实例。</returns>
        public FFmpegBuilder WithThreads(int threadCount)
        {
            _arguments.Append($" -threads {threadCount}");
            return this;
        }

        /// <summary>
        /// 添加自定义 FFmpeg 参数。
        /// </summary>
        /// <param name="arguments">原始 FFmpeg 命令行参数。</param>
        /// <returns>当前构建器实例。</returns>
        public FFmpegBuilder WithCustomArguments(string arguments)
        {
            if (!string.IsNullOrEmpty(arguments))
            {
                _arguments.Append(' ');
                _arguments.Append(arguments);
            }

            return this;
        }

        /// <summary>
        /// 配置输出到管道。库内部会创建命名管道（Windows: NamedPipeServerStream, Linux: pipe:{name}），
        /// 并将管道路径传递给 ffmpeg 作为输出目标。
        /// </summary>
        /// <param name="pipeName">可选的自定义管道名称。为 <c>null</c> 时自动生成 GUID 名称。</param>
        /// <returns>当前构建器实例。</returns>
        public FFmpegBuilder ToPipe(string? pipeName = null)
        {
            _usePipeOutput = true;
            _pipeName = pipeName ?? Guid.NewGuid().ToString("N");
            _targetPath = "pipe";
            return this;
        }

        /// <summary>
        /// 配置输出到管道并设置管道回调等选项。
        /// </summary>
        /// <param name="configure">管道配置委托，如 <c>pipe => pipe.WithCallback((data, meta) => { }).WithBufferCapacity(200)</c>。</param>
        /// <returns>当前构建器实例。</returns>
        public FFmpegBuilder ToPipe(Action<PipeTarget> configure)
        {
            if (configure == null)
                throw new ArgumentNullException(nameof(configure));

            var pipeTarget = new PipeTarget();
            configure(pipeTarget);

            _usePipeOutput = true;
            _pipeName = pipeTarget.PipeName;
            _targetPath = "pipe";
            _pipeDataCallback = pipeTarget.Callback;
            _bufferCapacity = pipeTarget.BufferCapacity;
            _pipeType = pipeTarget.PipeType;
            _frameAnalyzer = pipeTarget.FrameAnalyzer;

            return this;
        }

        /// <summary>
        /// 配置输出到文件。
        /// </summary>
        /// <param name="filePath">输出文件路径。</param>
        /// <returns>当前构建器实例。</returns>
        public FFmpegBuilder ToFile(string filePath)
        {
            _targetPath = filePath ?? throw new ArgumentNullException(nameof(filePath));
            _usePipeOutput = false;
            return this;
        }

        /// <summary>
        /// 配置输出到 RTSP 推流地址。
        /// </summary>
        /// <param name="rtspAddress">RTSP 推流地址，如 <c>rtsp://192.168.1.100:554/live/stream</c>。</param>
        /// <param name="transportProtocol">RTSP 传输协议，仅支持 "tcp"（默认）或 "udp"。</param>
        /// <returns>当前构建器实例。</returns>
        /// <exception cref="ArgumentNullException">当 <paramref name="rtspAddress"/> 为 <c>null</c> 或空时抛出。</exception>
        /// <exception cref="ArgumentException">当 <paramref name="transportProtocol"/> 不是 "tcp" 或 "udp" 时抛出。</exception>
        /// <remarks>
        /// 此方法会自动添加 <c>-rtsp_transport {protocol}</c> 和 <c>-f rtsp</c> 参数。
        /// 注意：如果在此之前调用了 <see cref="WithFormat"/> 设置了其他格式参数，
        /// 该参数将被 RTSP 推流格式（-f rtsp）覆盖并忽略。
        /// </remarks>
        public FFmpegBuilder ToRtsp(string rtspAddress, string transportProtocol = "tcp")
        {
            if (string.IsNullOrWhiteSpace(rtspAddress))
                throw new ArgumentNullException(nameof(rtspAddress));

            if (!ValidRtspOutputTransports.Contains(transportProtocol))
                throw new ArgumentException(
                    $"无效的 RTSP 传输协议: {transportProtocol}。允许的值: {string.Join(", ", ValidRtspOutputTransports)}",
                    nameof(transportProtocol));

            _isRtspOutput = true;
            _targetPath = rtspAddress;
            RemoveFormatArguments();
            _arguments.Append($" -rtsp_transport {transportProtocol} -f rtsp");

            return this;
        }

        /// <summary>
        /// 配置输出到网络地址（如 RTMP、UDP、SRT 等）。
        /// </summary>
        /// <param name="networkUrl">目标网络 URL。</param>
        /// <returns>当前构建器实例。</returns>
        public FFmpegBuilder ToNetwork(string networkUrl)
        {
            _targetPath = networkUrl ?? throw new ArgumentNullException(nameof(networkUrl));
            _usePipeOutput = false;
            return this;
        }

        /// <summary>
        /// 构建 <see cref="FFmpegRunner"/> 实例，将构建器配置的参数映射到 Runner 的相应属性。
        /// </summary>
        /// <returns>配置完成的 <see cref="FFmpegRunner"/> 实例。</returns>
        /// <exception cref="InvalidOperationException">当未设置源路径或输出目标时抛出。</exception>
        public FFmpegRunner Build()
        {
            if (string.IsNullOrEmpty(_sourcePath))
            {
                throw new InvalidOperationException("必须设置源路径（调用 FromSource() 或 FromRtspSource()）。");
            }

            if (string.IsNullOrEmpty(_targetPath) && !_usePipeOutput)
            {
                throw new InvalidOperationException("必须设置输出目标（调用 ToFile()、ToPipe()、ToNetwork() 或 ToRtsp()）。");
            }

            var commandArgs = BuildCommandArguments();
            var inputArgs = _inputOptions?.BuildArguments() ?? string.Empty;

            if (!_videoCodecExplicitlySet && !HasVideoCodecInCustomArgs(commandArgs))
            {
                var defaultCodec = " -c:v libx264";
                commandArgs = commandArgs + defaultCodec;
            }

            if (_pipeType == PipeType.Frame)
            {
                if (!commandArgs.Contains("-f "))
                    commandArgs += " -f h264";
                if (!commandArgs.Contains("-an"))
                    commandArgs += " -an";
            }

            if (_isRtspOutput && commandArgs.Contains("-f ") && !commandArgs.Contains("-f rtsp"))
            {
                throw new InvalidOperationException("RTSP 推流模式不允许使用其他格式参数。请仅使用 ToRtsp() 方法配置 RTSP 推流。");
            }

            IPipeInterface? pipe = null;

            if (_usePipeOutput)
            {
                pipe = CreatePipe(_pipeType, _pipeName, _frameAnalyzer);
                pipe.BufferCapacity = _bufferCapacity;
            }

            var runner = new FFmpegRunner(
                _ffmpegPath,
                _sourcePath,
                commandArgs,
                _targetPath,
                pipe)
            {
                InputArguments = inputArgs,
                Overwrite = _overwrite
            };

            if (pipe != null && _pipeDataCallback != null)
            {
                pipe.DataReceived += (_, e) => _pipeDataCallback(e.Data, e.Metadata);
            }

            return runner;
        }

        private static bool HasVideoCodecInCustomArgs(string arguments)
        {
            if (string.IsNullOrEmpty(arguments))
                return false;

            return Regex.IsMatch(arguments, @"-c:v\s+\S+") || Regex.IsMatch(arguments, @"-vcodec\s+\S+");
        }

        private static IPipeInterface CreatePipe(PipeType pipeType, string pipeName, IFrameAnalyzer? frameAnalyzer)
        {
            switch (pipeType)
            {
                case PipeType.Frame:
                    var framePipe = new FramePipe(pipeName);
                    if (frameAnalyzer != null)
                        framePipe.FrameAnalyzer = frameAnalyzer;
                    return framePipe;
                case PipeType.Stream:
                default:
                    return new StreamPipe(pipeName);
            }
        }

        private void RemoveFormatArguments()
        {
            var args = _arguments.ToString();
            var pattern = @"-f\s+\S+";
            var result = Regex.Replace(args, pattern, "");
            result = Regex.Replace(result, @"\s+", " ").Trim();

            _arguments.Clear();
            _arguments.Append(result);
        }

        private string BuildCommandArguments()
        {
            return _arguments.ToString().Trim();
        }
    }
}
