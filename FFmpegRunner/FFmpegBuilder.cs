using System;
using System.Text;

namespace FFmpegRunner
{
    /// <summary>
    /// FFmpeg 参数构建器，提供 Fluent API 风格的链式调用接口，
    /// 用于构建 FFmpeg 命令行参数并生成 <see cref="FFmpegRunner"/> 实例。
    /// </summary>
    public class FFmpegBuilder
    {
        private readonly StringBuilder _arguments = new StringBuilder();
        private string _sourcePath = string.Empty;
        private string? _ffmpegPath;
        private string _targetPath = string.Empty;
        private Action<byte[]>? _pipeDataCallback;
        private bool _overwrite;
        private bool _usePipeOutput;
        private string _pipeName = Guid.NewGuid().ToString("N");
        private int _bufferCapacity = 100;

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
        /// <returns>当前构建器实例。</returns>
        public FFmpegBuilder FromSource(string sourcePath)
        {
            _sourcePath = sourcePath ?? throw new ArgumentNullException(nameof(sourcePath));
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
        /// <param name="configure">管道配置委托，如 <c>pipe => pipe.WithCallback(data => { }).WithBufferCapacity(200)</c>。</param>
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
                throw new InvalidOperationException("必须设置源路径（调用 FromSource()）。");
            }

            if (string.IsNullOrEmpty(_targetPath) && !_usePipeOutput)
            {
                throw new InvalidOperationException("必须设置输出目标（调用 ToFile()、ToPipe() 或 ToNetwork()）。");
            }

            var commandArgs = BuildCommandArguments();

            var runner = new FFmpegRunner(
                _ffmpegPath,
                _sourcePath,
                commandArgs,
                _targetPath,
                _usePipeOutput,
                _pipeName);

            runner.PipeBufferCapacity = _bufferCapacity;

            if (_pipeDataCallback != null)
            {
                runner.PipeDataReceived += (_, e) => _pipeDataCallback(e.Data);
            }

            return runner;
        }

        private string BuildCommandArguments()
        {
            var sb = new StringBuilder();

            if (_overwrite)
            {
                sb.Append("-y ");
            }

            sb.Append(_arguments.ToString());

            return sb.ToString().Trim();
        }
    }
}