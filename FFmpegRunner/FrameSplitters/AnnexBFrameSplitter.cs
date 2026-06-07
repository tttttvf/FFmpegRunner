using System;
using System.Collections.Generic;

namespace FFmpegRunner
{
    /// <summary>
    /// Annex B 格式帧分割器抽象基类。提供 NAL 单元切分和 Access Unit 聚合的通用逻辑。
    /// 子类只需实现编解码器特定的 NAL 类型判断方法。
    /// </summary>
    /// <remarks>
    /// Annex B 格式使用起始码（0x00000001 或 0x000001）分隔 NAL 单元。
    /// 一个 Access Unit（帧）可能包含多个 NAL 单元（如 SPS/PPS + Slice）。
    /// </remarks>
    public abstract class AnnexBFrameSplitter : IFrameSplitter
    {
        private readonly List<byte[]> _pendingNalUnits = new List<byte[]>();
        private bool _pendingAuHasVcl;

        /// <inheritdoc />
        public bool TryExtractFrame(List<byte> buffer, out byte[]? frame)
        {
            frame = null;

            while (true)
            {
                var nalUnit = ExtractSingleNalUnit(buffer);
                if (nalUnit == null)
                    return false;

                var isVcl = IsVclNalUnit(nalUnit);
                var isBoundary = IsAuBoundary(nalUnit);

                if (_pendingAuHasVcl && (isBoundary || isVcl))
                {
                    frame = MergeNalUnits(_pendingNalUnits);
                    _pendingNalUnits.Clear();
                    _pendingAuHasVcl = false;

                    _pendingNalUnits.Add(nalUnit);
                    if (isVcl)
                        _pendingAuHasVcl = true;

                    return true;
                }

                _pendingNalUnits.Add(nalUnit);
                if (isVcl)
                    _pendingAuHasVcl = true;
            }
        }

        /// <inheritdoc />
        public bool TryFlush(out byte[]? frame)
        {
            if (_pendingNalUnits.Count > 0)
            {
                frame = MergeNalUnits(_pendingNalUnits);
                _pendingNalUnits.Clear();
                _pendingAuHasVcl = false;
                return true;
            }

            frame = null;
            return false;
        }

        /// <inheritdoc />
        public void Reset()
        {
            _pendingNalUnits.Clear();
            _pendingAuHasVcl = false;
        }

        /// <summary>
        /// 判断指定 NAL 单元是否为 VCL（Video Coding Layer）单元。
        /// </summary>
        /// <param name="nalUnit">NAL 单元字节数据（含起始码）。</param>
        /// <returns>如果是 VCL NAL 单元则返回 <c>true</c>。</returns>
        protected abstract bool IsVclNalUnit(byte[] nalUnit);

        /// <summary>
        /// 判断指定 NAL 单元是否为 Access Unit 边界标记。
        /// 当此方法返回 <c>true</c> 且当前已累积 VCL 单元时，将触发帧输出。
        /// </summary>
        /// <param name="nalUnit">NAL 单元字节数据（含起始码）。</param>
        /// <returns>如果是 AU 边界则返回 <c>true</c>。</returns>
        protected abstract bool IsAuBoundary(byte[] nalUnit);

        /// <summary>
        /// 从 NAL 单元中提取类型字节（起始码之后的第一个字节）。
        /// </summary>
        /// <param name="nalUnit">NAL 单元字节数据（含起始码）。</param>
        /// <returns>类型字节值，如果无法识别则返回 -1。</returns>
        protected static int GetNalUnitTypeByte(byte[] nalUnit)
        {
            for (var i = 0; i < nalUnit.Length - 2; i++)
            {
                if (IsStartCodeAt(nalUnit, i, out var scLen))
                    return nalUnit[i + scLen];
            }
            return -1;
        }

        /// <summary>
        /// 从缓冲区中提取一个完整的 NAL 单元（按 Annex B 起始码定位）。
        /// 提取成功后，对应的字节将从缓冲区中移除。
        /// </summary>
        private static byte[]? ExtractSingleNalUnit(List<byte> buffer)
        {
            var firstSC = FindAnnexBStartCode(buffer, 0);
            if (firstSC < 0)
                return null;

            if (firstSC > 0)
                buffer.RemoveRange(0, firstSC);

            var secondSC = FindAnnexBStartCode(buffer, 1);
            if (secondSC < 0)
                return null;

            var nalUnit = new byte[secondSC];
            buffer.CopyTo(0, nalUnit, 0, secondSC);
            buffer.RemoveRange(0, secondSC);
            return nalUnit;
        }

        /// <summary>
        /// 在缓冲区中查找 Annex B 起始码。
        /// </summary>
        private static int FindAnnexBStartCode(List<byte> buffer, int startIndex)
        {
            for (var i = startIndex; i < buffer.Count - 2; i++)
            {
                if (IsAnnexBStartCodeAt(buffer, i))
                    return i;
            }
            return -1;
        }

        /// <summary>
        /// 检查指定位置是否为 Annex B 起始码。
        /// 支持 4 字节（0x00000001）和 3 字节（0x000001）两种起始码格式。
        /// </summary>
        private static bool IsAnnexBStartCodeAt(List<byte> buffer, int index)
        {
            if (index + 3 < buffer.Count &&
                buffer[index] == 0x00 && buffer[index + 1] == 0x00 &&
                buffer[index + 2] == 0x00 && buffer[index + 3] == 0x01)
                return true;

            if (index + 2 < buffer.Count &&
                (index <= 0 || buffer[index - 1] != 0x00) &&
                buffer[index] == 0x00 && buffer[index + 1] == 0x00 && buffer[index + 2] == 0x01)
                return true;

            return false;
        }

        /// <summary>
        /// 检查字节数组指定位置是否为 Annex B 起始码（用于 NAL 单元内部解析）。
        /// </summary>
        private static bool IsStartCodeAt(byte[] data, int index, out int scLen)
        {
            scLen = 0;

            if (index + 3 < data.Length &&
                data[index] == 0x00 && data[index + 1] == 0x00 &&
                data[index + 2] == 0x00 && data[index + 3] == 0x01)
            {
                scLen = 4;
                return true;
            }

            if (index + 2 < data.Length &&
                data[index] == 0x00 && data[index + 1] == 0x00 && data[index + 2] == 0x01)
            {
                if (index <= 0 || data[index - 1] != 0x00)
                {
                    scLen = 3;
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// 将多个 NAL 单元合并为一个连续的字节数组（Access Unit）。
        /// </summary>
        private static byte[] MergeNalUnits(List<byte[]> nalUnits)
        {
            if (nalUnits.Count == 0)
                return Array.Empty<byte>();

            if (nalUnits.Count == 1)
                return nalUnits[0];

            var totalLength = 0;
            for (var i = 0; i < nalUnits.Count; i++)
                totalLength += nalUnits[i].Length;

            var result = new byte[totalLength];
            var offset = 0;
            for (var i = 0; i < nalUnits.Count; i++)
            {
                Buffer.BlockCopy(nalUnits[i], 0, result, offset, nalUnits[i].Length);
                offset += nalUnits[i].Length;
            }

            return result;
        }
    }
}