using System;

namespace FFmpegRunner
{
    /// <summary>
    /// MJPEG 帧数据分析器，通过 JPEG SOI/EOI 标记和帧内结构识别。
    /// </summary>
    public class MjpegFrameAnalyzer : IFrameAnalyzer
    {
        /// <inheritdoc />
        public bool TryAnalyze(byte[] data, out FrameMetadata? metadata)
        {
            metadata = null;

            if (data.Length < 4)
                return false;

            if (data[0] != 0xFF || data[1] != 0xD8)
                return false;

            if (data[data.Length - 2] == 0xFF && data[data.Length - 1] == 0xD9)
            {
                metadata = new FrameMetadata
                {
                    Size = data.Length,
                    Type = FrameType.I,
                    IsKeyFrame = true
                };
                return true;
            }

            for (var i = 2; i < data.Length - 1 && i < 1024; i++)
            {
                if (data[i] != 0xFF)
                    continue;

                var marker = data[i + 1];
                if (marker == 0xE0 || marker == 0xE1 || marker == 0xDB || marker == 0xC0 || marker == 0xC4)
                {
                    metadata = new FrameMetadata
                    {
                        Size = data.Length,
                        Type = FrameType.I,
                        IsKeyFrame = true
                    };
                    return true;
                }
            }

            return false;
        }

        /// <inheritdoc />
        public bool IsAudioFrame(byte[] data)
        {
            return false;
        }
    }
}