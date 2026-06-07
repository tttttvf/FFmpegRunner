namespace FFmpegRunner
{
    /// <summary>
    /// H.265 (HEVC) Annex B 帧分割器。
    /// 通过 NAL 单元类型识别 AU 边界，将 VPS/SPS/PPS + Slice 聚合为完整帧。
    /// </summary>
    public class H265FrameSplitter : AnnexBFrameSplitter
    {
        private const int Vps = 32;
        private const int Aud = 35;

        /// <inheritdoc />
        protected override bool IsVclNalUnit(byte[] nalUnit)
        {
            var typeByte = GetNalUnitTypeByte(nalUnit);
            if (typeByte < 0)
                return false;

            var nalType = (typeByte >> 1) & 0x3F;

            // VCL NAL 单元类型：0-15（普通 slice），16-21（IRAP）
            return nalType <= 21;
        }

        /// <inheritdoc />
        protected override bool IsAuBoundary(byte[] nalUnit)
        {
            var typeByte = GetNalUnitTypeByte(nalUnit);
            if (typeByte < 0)
                return false;

            var nalType = (typeByte >> 1) & 0x3F;

            // VPS 或 AUD 标记 AU 边界
            return nalType == Vps || nalType == Aud;
        }
    }
}