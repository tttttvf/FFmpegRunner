using System.Collections.Generic;

namespace FFmpegRunner
{
    /// <summary>
    /// 帧分割器接口。实现类负责从原始字节缓冲区中提取完整帧数据。
    /// 多个 <see cref="IFrameSplitter"/> 可通过 <see cref="CompositeFrameSplitter"/> 组合为责任链。
    /// </summary>
    /// <remarks>
    /// 设计上与 <see cref="IFrameAnalyzer"/> 对称：分析器负责解析帧内容（类型、关键帧等），
    /// 分割器负责从字节流中界定帧边界。两者可独立扩展。
    /// </remarks>
    public interface IFrameSplitter
    {
        /// <summary>
        /// 尝试从字节缓冲区中提取一个完整帧。
        /// 提取成功后，已消费的字节将从缓冲区中移除。
        /// </summary>
        /// <param name="buffer">待处理的字节缓冲区。成功提取后，已消费的字节将被移除。</param>
        /// <param name="frame">提取到的完整帧数据。提取失败时为 <c>null</c>。</param>
        /// <returns>成功提取帧数据时返回 <c>true</c>，否则返回 <c>false</c>。</returns>
        bool TryExtractFrame(List<byte> buffer, out byte[]? frame);

        /// <summary>
        /// 刷新内部缓冲区中尚未完成的帧数据。
        /// 在流结束或管道关闭时调用，确保不丢失部分数据。
        /// </summary>
        /// <param name="frame">刷新的帧数据。无待处理数据时为 <c>null</c>。</param>
        /// <returns>有待处理数据被刷新时返回 <c>true</c>。</returns>
        bool TryFlush(out byte[]? frame);

        /// <summary>
        /// 重置内部状态，丢弃所有待处理的中间数据。
        /// </summary>
        void Reset();
    }
}