using System;
using System.Threading;
using System.Threading.Tasks;

namespace FFmpegRunner
{
    /// <summary>
    /// 定义管道接口的抽象，所有管道实现类必须遵循此契约。
    /// 提供标准化的管道生命周期管理和数据事件处理。
    /// </summary>
    public interface IPipeInterface : IDisposable
    {
        /// <summary>
        /// 获取当前管道的名称。
        /// </summary>
        string PipeName { get; }

        /// <summary>
        /// 获取或设置管道数据缓冲区的容量（数据块数）。必须在 <see cref="Initialize"/> 之前设置。
        /// </summary>
        int BufferCapacity { get; set; }

        /// <summary>
        /// 当管道读取到新的数据时触发。
        /// <list type="bullet">
        ///   <item>对于 <see cref="StreamPipe"/>：此事件的 <see cref="FrameEventArgs.Metadata"/> 始终为 <c>null</c>。</item>
        ///   <item>对于 <see cref="FramePipe"/>：此事件的 <see cref="FrameEventArgs.Metadata"/> 包含帧元数据（如可用）。</item>
        /// </list>
        /// </summary>
        event EventHandler<FrameEventArgs>? DataReceived;

        /// <summary>
        /// 获取适用于 FFmpeg 的输出目标路径。
        /// </summary>
        /// <returns>格式化后的管道目标路径，如 Windows 上的 <c>\\.\pipe\{name}</c> 或 Linux 上的 <c>pipe:{name}</c>。</returns>
        string GetOutputTarget();

        /// <summary>
        /// 初始化管道服务端，创建底层 <see cref="System.IO.Pipes.NamedPipeServerStream"/> 和数据通道。
        /// 必须在调用 <see cref="Start"/> 之前调用此方法。
        /// </summary>
        void Initialize();

        /// <summary>
        /// 启动管道的读取和消费任务。
        /// </summary>
        /// <param name="cancellationToken">用于取消管道操作的令牌。</param>
        void Start(CancellationToken cancellationToken);

        /// <summary>
        /// 停止管道的读取和消费任务。
        /// </summary>
        void Stop();
    }
}
