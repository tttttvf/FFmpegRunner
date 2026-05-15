using System;

namespace FFmpegRunner
{
    /// <summary>
    /// H.264 (AVC) 帧数据分析器，通过 Annex B NAL 单元解析识别帧类型和关键帧。
    /// </summary>
    public class H264FrameAnalyzer : IFrameAnalyzer
    {
        private const byte NalTypeMask = 0x1F;
        private const byte IdrSlice = 5;
        private const byte NonIdrSlice = 1;
        private const byte Sps = 7;

        /// <inheritdoc />
        public bool TryAnalyze(byte[] data, out FrameMetadata? metadata)
        {
            metadata = null;

            if (data.Length < 5)
                return false;

            var foundVcl = false;
            var hasIdr = false;
            var hasSps = false;

            for (var i = 0; i < data.Length - 4; i++)
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

                var nalType = data[nalPos] & NalTypeMask;

                switch (nalType)
                {
                    case IdrSlice:
                        hasIdr = true;
                        foundVcl = true;
                        break;
                    case NonIdrSlice:
                        foundVcl = true;
                        break;
                    case Sps:
                        hasSps = true;
                        break;
                    default:
                        if (nalType >= 1 && nalType <= 5)
                            foundVcl = true;
                        break;
                }

                i += startCodeLength;
            }

            if (!foundVcl)
                return false;

            var isKeyFrame = hasIdr || hasSps;

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