using System;

namespace FFmpegRunner
{
    /// <summary>
    /// 帧数据分析器接口。实现类负责从原始字节数据中识别帧类型、关键帧标志等元数据。
    /// 多个 <see cref="IFrameAnalyzer"/> 可通过 <see cref="CompositeFrameAnalyzer"/> 组合为责任链。
    /// </summary>
    public interface IFrameAnalyzer
    {
        /// <summary>
        /// 尝试分析帧数据并提取元数据。
        /// </summary>
        /// <param name="data">原始帧字节数据。</param>
        /// <param name="metadata">分析结果。当方法返回 <c>true</c> 时包含完整的元数据；返回 <c>false</c> 时值为 <c>null</c>。</param>
        /// <returns>如果当前分析器能够识别该帧数据则返回 <c>true</c>，否则返回 <c>false</c> 交由责任链中的下一个分析器处理。</returns>
        bool TryAnalyze(byte[] data, out FrameMetadata? metadata);

        /// <summary>
        /// 检查帧数据是否为当前分析器支持的音频帧格式。
        /// </summary>
        /// <param name="data">原始帧字节数据。</param>
        /// <returns>如果识别为音频帧则返回 <c>true</c>。</returns>
        bool IsAudioFrame(byte[] data);
    }
}