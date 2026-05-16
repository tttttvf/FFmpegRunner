namespace FFmpegRunner
{
    /// <summary>
    /// 指定管道的数据传输模式类型。
    /// </summary>
    public enum PipeType
    {
        /// <summary>
        /// 流模式。直接透传管道读取的原始字节数据，不进行任何协议解析。
        /// 适用于需要直接处理 FFmpeg 原始输出的场景。
        /// </summary>
        Stream,

        /// <summary>
        /// 帧模式。采用长度前缀帧协议进行数据传输。
        /// 每次传输先发送4字节的长度字段（BigEndian），再发送对应的帧数据。
        /// 适用于需要按帧解析数据的高级场景。
        /// </summary>
        Frame
    }
}