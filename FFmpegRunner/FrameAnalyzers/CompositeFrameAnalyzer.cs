using System;
using System.Collections.Generic;
using System.Linq;

namespace FFmpegRunner
{
    /// <summary>
    /// 复合帧分析器，按责任链模式依次尝试多个 <see cref="IFrameAnalyzer"/>，
    /// 返回第一个成功识别的结果。同时负责音频帧检测。
    /// </summary>
    public class CompositeFrameAnalyzer : IFrameAnalyzer
    {
        private readonly List<IFrameAnalyzer> _analyzers;

        /// <summary>
        /// 初始化复合帧分析器，使用默认分析器集合（H.264、H.265、MJPEG）。
        /// </summary>
        public CompositeFrameAnalyzer()
            : this(new H264FrameAnalyzer(), new H265FrameAnalyzer(), new MjpegFrameAnalyzer())
        {
        }

        /// <summary>
        /// 初始化复合帧分析器，使用指定的分析器集合。
        /// 分析器按传入顺序尝试，建议将最常见编码排在前面以获得最佳性能。
        /// </summary>
        /// <param name="analyzers">按顺序尝试的帧分析器集合。</param>
        public CompositeFrameAnalyzer(params IFrameAnalyzer[] analyzers)
        {
            _analyzers = analyzers?.ToList() ?? throw new ArgumentNullException(nameof(analyzers));
        }

        /// <summary>
        /// 获取当前已注册的分析器数量。
        /// </summary>
        public int Count => _analyzers.Count;

        /// <summary>
        /// 向责任链末尾添加一个分析器。
        /// </summary>
        public void AddAnalyzer(IFrameAnalyzer analyzer)
        {
            if (analyzer == null)
                throw new ArgumentNullException(nameof(analyzer));

            _analyzers.Add(analyzer);
        }

        /// <summary>
        /// 从责任链中移除指定类型的分析器。
        /// </summary>
        public bool RemoveAnalyzer<T>() where T : IFrameAnalyzer
        {
            var count = _analyzers.RemoveAll(a => a is T);
            return count > 0;
        }

        /// <inheritdoc />
        public bool TryAnalyze(byte[] data, out FrameMetadata? metadata)
        {
            foreach (var analyzer in _analyzers)
            {
                if (analyzer.TryAnalyze(data, out metadata))
                    return true;
            }

            metadata = null;
            return false;
        }

        /// <inheritdoc />
        public bool IsAudioFrame(byte[] data)
        {
            if (data.Length < 4)
                return false;

            if ((data[0] == 0xFF && (data[1] & 0xE0) == 0xE0))
                return true;

            if (data.Length >= 2
                && data[0] == 0x0B
                && data[1] == 0x77)
                return true;

            if (data[0] == 0xFF && data[1] == 0xF1)
                return true;

            if (data[0] == 0xFF && data[1] == 0xF9)
                return true;

            return false;
        }
    }
}