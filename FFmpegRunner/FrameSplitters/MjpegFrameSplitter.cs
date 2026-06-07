using System.Collections.Generic;

namespace FFmpegRunner
{
    /// <summary>
    /// MJPEG 帧分割器。通过 JPEG SOI (0xFFD8) 和 EOI (0xFFD9) 标记界定帧边界。
    /// 每个 MJPEG 帧都是独立的关键帧，无需跨帧聚合。
    /// </summary>
    public class MjpegFrameSplitter : IFrameSplitter
    {
        private const byte JpegSoiMarker = 0xD8;
        private const byte JpegEoiMarker = 0xD9;
        private const byte JpegMarkerPrefix = 0xFF;

        /// <inheritdoc />
        public bool TryExtractFrame(List<byte> buffer, out byte[]? frame)
        {
            frame = null;

            // 查找 SOI 标记
            var soiIndex = -1;
            for (var i = 0; i < buffer.Count - 1; i++)
            {
                if (buffer[i] == JpegMarkerPrefix && buffer[i + 1] == JpegSoiMarker)
                {
                    soiIndex = i;
                    break;
                }
            }

            if (soiIndex < 0)
                return false;

            // 移除 SOI 之前的数据
            if (soiIndex > 0)
                buffer.RemoveRange(0, soiIndex);

            // 查找 EOI 标记
            for (var j = 2; j < buffer.Count - 1; j++)
            {
                if (buffer[j] == JpegMarkerPrefix && buffer[j + 1] == JpegEoiMarker)
                {
                    frame = new byte[j + 2];
                    buffer.CopyTo(0, frame, 0, j + 2);
                    buffer.RemoveRange(0, j + 2);
                    return true;
                }
            }

            return false;
        }

        /// <inheritdoc />
        public bool TryFlush(out byte[]? frame)
        {
            frame = null;
            return false;
        }

        /// <inheritdoc />
        public void Reset()
        {
        }
    }
}