using System;

namespace FFmpegRunner
{
    /// <summary>
    /// 管道输出目标的 Fluent API 配置类。
    /// 通过 <see cref="FFmpegBuilder.ToPipe(System.Action{PipeTarget})"/> 进行链式配置，
    /// 可设置数据回调、缓冲区容量、管道名称和管道类型。
    /// </summary>
    public class PipeTarget
    {
        internal string PipeName { get; private set; } = Guid.NewGuid().ToString("N");
        internal Action<byte[], FrameMetadata?>? Callback { get; private set; }
        internal int BufferCapacity { get; private set; } = 100;
        internal PipeType PipeType { get; private set; } = PipeType.Stream;

        /// <summary>
        /// 设置管道数据回调处理函数。
        /// </summary>
        /// <param name="callback">处理管道数据的回调函数。第一个参数为原始字节数据，第二个参数为帧元数据（流管道为 null）。</param>
        /// <returns>当前配置实例。</returns>
        /// <example>
        /// <code>
        /// .WithCallback((data, metadata) =>
        /// {
        ///     Console.WriteLine($"收到 {data.Length} 字节");
        ///     if (metadata != null)
        ///     {
        ///         Console.WriteLine($"帧类型: {metadata.Type}");
        ///     }
        /// })
        /// </code>
        /// </example>
        public PipeTarget WithCallback(Action<byte[], FrameMetadata?> callback)
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

        /// <summary>
        /// 设置管道类型（流管道或帧管道）。默认为 <see cref="PipeType.Stream"/>。
        /// </summary>
        /// <param name="pipeType">管道类型。</param>
        /// <returns>当前配置实例。</returns>
        public PipeTarget WithPipeType(PipeType pipeType)
        {
            PipeType = pipeType;
            return this;
        }
    }
}
