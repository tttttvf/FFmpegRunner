namespace FFmpegRunner
{
    /// <summary>
    /// 表示帧的元数据信息，包括时间戳、帧类型、分辨率等。
    /// </summary>
    public class FrameMetadata
    {
        /// <summary>
        /// 获取或设置帧的显示时间戳（PTS），以微秒为单位。
        /// </summary>
        public long Timestamp { get; set; }

        /// <summary>
        /// 获取或设置帧的解码时间戳（DTS），以微秒为单位。
        /// </summary>
        public long DecodeTimestamp { get; set; }

        /// <summary>
        /// 获取或设置帧类型。
        /// </summary>
        public FrameType Type { get; set; } = FrameType.Unknown;

        /// <summary>
        /// 获取或设置视频帧的宽度（像素）。
        /// </summary>
        public int Width { get; set; }

        /// <summary>
        /// 获取或设置视频帧的高度（像素）。
        /// </summary>
        public int Height { get; set; }

        /// <summary>
        /// 获取或设置帧是否为关键帧（I 帧）。
        /// </summary>
        public bool IsKeyFrame { get; set; }

        /// <summary>
        /// 获取或设置帧数据的大小（字节）。
        /// </summary>
        public int Size { get; set; }
    }

    /// <summary>
    /// 表示视频帧的类型。
    /// </summary>
    public enum FrameType
    {
        /// <summary>
        /// 未知帧类型。
        /// </summary>
        Unknown = 0,

        /// <summary>
        /// I 帧（关键帧），可独立解码。
        /// </summary>
        I = 1,

        /// <summary>
        /// P 帧（预测帧），依赖前向参考帧。
        /// </summary>
        P = 2,

        /// <summary>
        /// B 帧（双向预测帧），依赖前后参考帧。
        /// </summary>
        B = 3,

        /// <summary>
        /// 音频帧。
        /// </summary>
        Audio = 4,

        /// <summary>
        /// 字幕帧。
        /// </summary>
        Subtitle = 5
    }
}
