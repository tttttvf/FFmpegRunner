using System;

namespace FFmpegRunner
{
    /// <summary>
    /// 为管道数据到达事件提供数据。
    /// </summary>
    public class FrameEventArgs : EventArgs
    {
        /// <summary>
        /// 初始化 <see cref="FrameEventArgs"/> 类的新实例。
        /// </summary>
        /// <param name="data">从管道读取的原始字节数据。</param>
        /// <param name="metadata">帧元数据信息。对于流管道，此值可能为 <c>null</c>。</param>
        public FrameEventArgs(byte[] data, FrameMetadata? metadata = null)
        {
            Data = data;
            Metadata = metadata;
        }

        /// <summary>
        /// 获取从管道读取的原始字节数据。
        /// </summary>
        public byte[] Data { get; }

        /// <summary>
        /// 获取帧的元数据信息。
        /// 对于 <see cref="StreamPipe"/>，此属性始终为 <c>null</c>。
        /// 对于 <see cref="FramePipe"/>，此属性包含解析出的帧元数据。
        /// </summary>
        public FrameMetadata? Metadata { get; }
    }
}
