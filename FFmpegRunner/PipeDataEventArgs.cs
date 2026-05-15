using System;

namespace FFmpegRunner
{
    /// <summary>
    /// 为 <see cref="FFmpegRunner.PipeDataReceived"/> 事件提供数据。
    /// </summary>
    public class PipeDataEventArgs : EventArgs
    {
        /// <summary>
        /// 初始化 <see cref="PipeDataEventArgs"/> 类的新实例。
        /// </summary>
        /// <param name="data">从管道读取的原始字节数据。</param>
        public PipeDataEventArgs(byte[] data)
        {
            Data = data;
        }

        /// <summary>
        /// 获取从管道读取的原始字节数据。
        /// </summary>
        public byte[] Data { get; }
    }
}