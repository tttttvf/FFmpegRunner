namespace FFmpegRunner
{
    /// <summary>
    /// H.264 (AVC) Annex B 帧分割器。
    /// 通过 NAL 单元类型识别 AU 边界，将 SPS/PPS + Slice 聚合为完整帧。
    /// </summary>
    public class H264FrameSplitter : AnnexBFrameSplitter
    {
        private const byte NalTypeMask = 0x1F;
        private const byte IdrSlice = 5;
        private const byte NonIdrSlice = 1;
        private const byte Sps = 7;
        private const byte Aud = 9;

        /// <inheritdoc />
        protected override bool IsVclNalUnit(byte[] nalUnit)
        {
            var typeByte = GetNalUnitTypeByte(nalUnit);
            if (typeByte < 0)
                return false;

            var nalType = typeByte & NalTypeMask;
            return nalType >= 1 && nalType <= 5;
        }

        /// <inheritdoc />
        protected override bool IsAuBoundary(byte[] nalUnit)
        {
            var typeByte = GetNalUnitTypeByte(nalUnit);
            if (typeByte < 0)
                return false;

            var nalType = typeByte & NalTypeMask;

            // SPS 或 AUD 标记 AU 边界
            return nalType == Sps || nalType == Aud;
        }
    }
}