using System.Text;

namespace FFmpegRunner
{
    /// <summary>
    /// FFmpeg 输入参数配置类，提供 Fluent API 风格的链式调用接口。
    /// 用于在 <see cref="FFmpegBuilder.FromSource"/> 和 <see cref="FFmpegBuilder.FromRtspSource"/>
    /// 的回调中配置输入源相关参数，这些参数会被放置在 <c>-i</c> 之前。
    /// </summary>
    /// <example>
    /// <code>
    /// new FFmpegBuilder()
    ///     .FromSource("input.mp4", opt => opt
    ///         .WithInputFrameRate(30)
    ///         .WithInputHardwareAcceleration("cuda"))
    ///     .WithVideoCodec("h264")
    ///     .ToFile("output.mp4")
    ///     .Build();
    /// </code>
    /// </example>
    public class InputOptions
    {
        private readonly StringBuilder _arguments;

        internal InputOptions()
        {
            _arguments = new StringBuilder();
        }

        internal InputOptions(StringBuilder arguments)
        {
            _arguments = arguments;
        }

        internal string BuildArguments()
        {
            return _arguments.ToString().Trim();
        }

        /// <summary>
        /// 设置输入帧率（-r）。覆盖 FFmpeg 对输入源的帧率检测结果。
        /// </summary>
        /// <param name="frameRate">输入帧率值。</param>
        /// <returns>当前配置实例。</returns>
        public InputOptions WithFrameRate(int frameRate)
        {
            _arguments.Append($" -r {frameRate}");
            return this;
        }

        /// <summary>
        /// 设置输入视频解码器（-c:v）。用于指定输入源的视频解码器，如硬件加速解码器。
        /// </summary>
        /// <param name="codec">解码器名称（如 h264_cuvid、hevc_cuvid）。</param>
        /// <returns>当前配置实例。</returns>
        public InputOptions WithVideoCodec(string codec)
        {
            _arguments.Append($" -c:v {codec}");
            return this;
        }

        /// <summary>
        /// 设置输入音频解码器（-c:a）。用于指定输入源的音频解码器。
        /// </summary>
        /// <param name="codec">解码器名称（如 aac、mp3、opus）。</param>
        /// <returns>当前配置实例。</returns>
        public InputOptions WithAudioCodec(string codec)
        {
            _arguments.Append($" -c:a {codec}");
            return this;
        }

        /// <summary>
        /// 设置输入缓冲区大小（-buffer_size）。控制 FFmpeg 读取输入流时的内部缓冲区大小。
        /// </summary>
        /// <param name="bufferSize">缓冲区大小（字节）。</param>
        /// <returns>当前配置实例。</returns>
        public InputOptions WithBufferSize(int bufferSize)
        {
            _arguments.Append($" -buffer_size {bufferSize}");
            return this;
        }

        /// <summary>
        /// 设置输入超时时间（-timeout）。控制 FFmpeg 等待输入源数据时的超时。
        /// </summary>
        /// <param name="timeoutMicroseconds">超时时间（微秒）。</param>
        /// <returns>当前配置实例。</returns>
        public InputOptions WithTimeout(long timeoutMicroseconds)
        {
            _arguments.Append($" -timeout {timeoutMicroseconds}");
            return this;
        }

        /// <summary>
        /// 设置输入格式（-f）。显式指定输入源的容器格式，当 FFmpeg 无法自动检测时使用。
        /// </summary>
        /// <param name="format">格式名称（如 flv、matroska、rawvideo、h264）。</param>
        /// <returns>当前配置实例。</returns>
        public InputOptions WithFormat(string format)
        {
            _arguments.Append($" -f {format}");
            return this;
        }

        /// <summary>
        /// 设置硬件加速方式（-hwaccel）。启用硬件加速解码。
        /// </summary>
        /// <param name="hwaccel">硬件加速方式枚举值。</param>
        /// <returns>当前配置实例。</returns>
        public InputOptions WithHardwareAcceleration(HardwareAccelerationType hwaccel)
        {
            var value = hwaccel switch
            {
                HardwareAccelerationType.Auto => "auto",
                HardwareAccelerationType.Vdpau => "vdpau",
                HardwareAccelerationType.Dxva2 => "dxva2",
                HardwareAccelerationType.D3d11va => "d3d11va",
                HardwareAccelerationType.Vaapi => "vaapi",
                HardwareAccelerationType.Qsv => "qsv",
                HardwareAccelerationType.Amf => "amf",
                _ => "auto"
            };
            _arguments.Append($" -hwaccel {value}");
            return this;
        }

        /// <summary>
        /// 设置硬件加速方式（-hwaccel），使用自定义字符串参数。
        /// 当 <see cref="HardwareAccelerationType"/> 枚举未包含所需的加速方式时使用此重载，
        /// 例如 <c>"cuda"</c>、<c>"videotoolbox"</c>、<c>"mediacodec"</c> 等。
        /// </summary>
        /// <param name="hwaccel">硬件加速方式名称。</param>
        /// <returns>当前配置实例。</returns>
        public InputOptions WithHardwareAcceleration(string hwaccel)
        {
            _arguments.Append($" -hwaccel {hwaccel}");
            return this;
        }

        /// <summary>
        /// 禁用输入视频流（-vn）。从输入中跳过视频流的解码。
        /// </summary>
        /// <returns>当前配置实例。</returns>
        public InputOptions WithNoVideo()
        {
            _arguments.Append(" -vn");
            return this;
        }

        /// <summary>
        /// 禁用输入音频流（-an）。从输入中跳过音频流的解码。
        /// </summary>
        /// <returns>当前配置实例。</returns>
        public InputOptions WithNoAudio()
        {
            _arguments.Append(" -an");
            return this;
        }

        /// <summary>
        /// 设置输入快进位置（-ss）。当 -ss 位于 -i 之前时，使用关键帧快速定位。
        /// </summary>
        /// <param name="position">时间位置（如 "00:01:30" 或 "90"）。</param>
        /// <returns>当前配置实例。</returns>
        public InputOptions WithSeekPosition(string position)
        {
            _arguments.Append($" -ss {position}");
            return this;
        }

        /// <summary>
        /// 设置输入读取时长限制（-t）。限制从输入源读取的最大时长。
        /// </summary>
        /// <param name="duration">时长（如 "00:05:00" 或 "300"）。</param>
        /// <returns>当前配置实例。</returns>
        public InputOptions WithDuration(string duration)
        {
            _arguments.Append($" -t {duration}");
            return this;
        }

        /// <summary>
        /// 设置 RTSP 传输协议（-rtsp_transport）。指定 RTSP 流的传输方式。
        /// </summary>
        /// <param name="transport">传输协议（tcp、udp、http）。</param>
        /// <returns>当前配置实例。</returns>
        public InputOptions WithRtspTransport(string transport)
        {
            _arguments.Append($" -rtsp_transport {transport}");
            return this;
        }

        /// <summary>
        /// 添加自定义 FFmpeg 输入参数。
        /// </summary>
        /// <param name="arguments">原始 FFmpeg 命令行参数。</param>
        /// <returns>当前配置实例。</returns>
        public InputOptions WithCustomArguments(string arguments)
        {
            if (!string.IsNullOrEmpty(arguments))
            {
                _arguments.Append(' ');
                _arguments.Append(arguments);
            }

            return this;
        }
    }
}