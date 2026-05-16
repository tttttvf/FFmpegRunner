using System;

namespace FFmpegRunner
{
    /// <summary>
    /// H.265 (HEVC) 帧数据分析器，通过 Annex B NAL 单元解析识别帧类型和关键帧。
    /// </summary>
    public class H265FrameAnalyzer : IFrameAnalyzer
    {
        /// <inheritdoc />
        public bool TryAnalyze(byte[] data, out FrameMetadata? metadata)
        {
            metadata = null;

            if (data.Length < 6)
                return false;

            var foundVcl = false;
            var hasIdr = false;

            for (var i = 0; i < data.Length - 5; i++)
            {
                if (data[i] != 0x00 || data[i + 1] != 0x00)
                    continue;

                var startCodeLength = 0;

                if (data[i + 2] == 0x00 && data[i + 3] == 0x01)
                {
                    startCodeLength = 4;
                }
                else if (data[i + 2] == 0x01)
                {
                    startCodeLength = 3;
                }

                if (startCodeLength == 0)
                    continue;

                var nalPos = i + startCodeLength;
                if (nalPos >= data.Length)
                    break;

                var nalUnitType = (data[nalPos] >> 1) & 0x3F;

                if (nalUnitType >= 16 && nalUnitType <= 21)
                {
                    hasIdr = true;
                    foundVcl = true;
                }
                else if (nalUnitType <= 15)
                {
                    foundVcl = true;
                }

                i += startCodeLength;
            }

            if (!foundVcl)
                return false;

            var isKeyFrame = hasIdr;

            metadata = new FrameMetadata
            {
                Size = data.Length,
                Type = isKeyFrame ? FrameType.I : FrameType.P,
                IsKeyFrame = isKeyFrame
            };

            return true;
        }

        /// <inheritdoc />
        public bool IsAudioFrame(byte[] data)
        {
            return false;
        }
    }
}