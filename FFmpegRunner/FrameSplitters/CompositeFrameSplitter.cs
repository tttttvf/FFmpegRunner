using System;
using System.Collections.Generic;
using System.Linq;

namespace FFmpegRunner
{
    /// <summary>
    /// 复合帧分割器，按责任链模式依次尝试多个 <see cref="IFrameSplitter"/>，
    /// 返回第一个成功提取的帧。与 <see cref="CompositeFrameAnalyzer"/> 架构对称。
    /// </summary>
    /// <remarks>
    /// 默认包含 H.264、H.265、MJPEG 分割器，按顺序尝试。
    /// 可通过构造函数自定义分割器集合，或通过 <see cref="AddSplitter"/> 动态扩展。
    /// </remarks>
    public class CompositeFrameSplitter : IFrameSplitter
    {
        private readonly List<IFrameSplitter> _splitters;

        /// <summary>
        /// 初始化复合帧分割器，使用默认分割器集合（H.264、H.265、MJPEG）。
        /// </summary>
        public CompositeFrameSplitter()
            : this(new H264FrameSplitter(), new H265FrameSplitter(), new MjpegFrameSplitter())
        {
        }

        /// <summary>
        /// 初始化复合帧分割器，使用指定的分割器集合。
        /// 分割器按传入顺序尝试，建议将最常见编码排在前面以获得最佳性能。
        /// </summary>
        /// <param name="splitters">按顺序尝试的帧分割器集合。</param>
        public CompositeFrameSplitter(params IFrameSplitter[] splitters)
        {
            _splitters = splitters?.ToList() ?? throw new ArgumentNullException(nameof(splitters));
        }

        /// <summary>
        /// 获取当前已注册的分割器数量。
        /// </summary>
        public int Count => _splitters.Count;

        /// <summary>
        /// 向责任链末尾添加一个分割器。
        /// </summary>
        public void AddSplitter(IFrameSplitter splitter)
        {
            if (splitter == null)
                throw new ArgumentNullException(nameof(splitter));

            _splitters.Add(splitter);
        }

        /// <summary>
        /// 从责任链中移除指定类型的分割器。
        /// </summary>
        public bool RemoveSplitter<T>() where T : IFrameSplitter
        {
            var count = _splitters.RemoveAll(s => s is T);
            return count > 0;
        }

        /// <inheritdoc />
        public bool TryExtractFrame(List<byte> buffer, out byte[]? frame)
        {
            foreach (var splitter in _splitters)
            {
                if (splitter.TryExtractFrame(buffer, out frame))
                    return true;
            }

            frame = null;
            return false;
        }

        /// <inheritdoc />
        public bool TryFlush(out byte[]? frame)
        {
            foreach (var splitter in _splitters)
            {
                if (splitter.TryFlush(out frame))
                    return true;
            }

            frame = null;
            return false;
        }

        /// <inheritdoc />
        public void Reset()
        {
            foreach (var splitter in _splitters)
            {
                splitter.Reset();
            }
        }
    }
}