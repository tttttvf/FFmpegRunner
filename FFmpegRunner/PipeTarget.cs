using System;

namespace FFmpegRunner
{
    /// <summary>
    /// 管道输出目标的 Fluent API 配置类。
    /// 通过 <see cref="FFmpegBuilder.ToPipe(System.Action{PipeTarget})"/> 进行链式配置，
    /// 可设置数据回调、缓冲区容量和管道名称。
    /// </summary>
    public class PipeTarget
    {
        internal string PipeName { get; private set; } = Guid.NewGuid().ToString("N");
        internal Action<byte[]>? Callback { get; private set; }
        internal int BufferCapacity { get; private set; } = 100;

        /// <summary>
        /// 设置管道数据回调处理函数。
        /// </summary>
        /// <param name="callback">处理管道数据的回调函数。</param>
        /// <returns>当前配置实例。</returns>
        public PipeTarget WithCallback(Action<byte[]> callback)
        {
            Callback = callback ?? throw new ArgumentNullException(nameof(callback));
            return this;
        }

        /// <summary>
        /// 设置管道数据缓冲区容量（数据块数）。
        /// </summary>
        /// <param name="capacity">缓冲区容量，最小值为 1。</param>
        /// <returns>当前配置实例。</returns>
        public PipeTarget WithBufferCapacity(int capacity)
        {
            BufferCapacity = Math.Max(1, capacity);
            return this;
        }

        /// <summary>
        /// 设置自定义管道名称。
        /// </summary>
        /// <param name="name">管道名称。在 Windows 上映射到 <c>\\.\pipe\{name}</c>，Linux 上映射到 <c>pipe:{name}</c>。</param>
        /// <returns>当前配置实例。</returns>
        public PipeTarget WithPipeName(string name)
        {
            PipeName = name ?? throw new ArgumentNullException(nameof(name));
            return this;
        }
    }
}