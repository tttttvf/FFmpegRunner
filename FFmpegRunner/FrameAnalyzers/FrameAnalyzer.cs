using System;

namespace FFmpegRunner
{
    /// <summary>
    /// 帧数据静态分析器，提供帧类型识别和音频帧检测的便捷入口。
    /// 内部委托给默认的 <see cref="CompositeFrameAnalyzer"/>（包含 H.264、H.265、MJPEG 分析器）。
    /// </summary>
    public static class FrameAnalyzer
    {
        private static readonly CompositeFrameAnalyzer DefaultAnalyzer = new CompositeFrameAnalyzer();

        /// <summary>
        /// 分析帧数据并返回元数据。如果无法识别帧类型则返回默认元数据。
        /// </summary>
        /// <param name="frameData">原始帧字节数据。</param>
        /// <returns>包含帧类型、关键帧标志和尺寸的元数据。</returns>
        public static FrameMetadata Analyze(byte[] frameData)
        {
            if (DefaultAnalyzer.TryAnalyze(frameData, out var metadata) && metadata != null)
                return metadata;

            return new FrameMetadata { Size = frameData.Length };
        }

        /// <summary>
        /// 检测给定的字节数据是否为音频帧（MP3、AAC ADTS、AC3 等）。
        /// </summary>
        /// <param name="frameData">原始帧字节数据。</param>
        /// <returns>如果是已知音频帧格式则返回 <c>true</c>。</returns>
        public static bool IsAudioFrame(byte[] frameData)
        {
            return DefaultAnalyzer.IsAudioFrame(frameData);
        }
    }
}