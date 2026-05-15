using System.Collections.Generic;

namespace FFmpegRunner
{
    /// <summary>
    /// 表示媒体流的元数据信息。
    /// </summary>
    public class StreamInfo
    {
        /// <summary>
        /// 获取或设置媒体流的索引。
        /// </summary>
        public int Index { get; set; }

        /// <summary>
        /// 获取或设置编码类型（video、audio、subtitle、data）。
        /// </summary>
        public string CodecType { get; set; } = string.Empty;

        /// <summary>
        /// 获取或设置编码格式名称（如 h264、aac）。
        /// </summary>
        public string CodecName { get; set; } = string.Empty;

        /// <summary>
        /// 获取或设置视频宽度（仅视频流有效）。
        /// </summary>
        public int? Width { get; set; }

        /// <summary>
        /// 获取或设置视频高度（仅视频流有效）。
        /// </summary>
        public int? Height { get; set; }

        /// <summary>
        /// 获取或设置像素格式（仅视频流有效）。
        /// </summary>
        public string? PixFmt { get; set; }

        /// <summary>
        /// 获取或设置帧率（仅视频流有效），以分数形式表示（如 "30/1"）。
        /// </summary>
        public string? RFrameRate { get; set; }

        /// <summary>
        /// 获取或设置比特率（单位：bps）。
        /// </summary>
        public long? BitRate { get; set; }

        /// <summary>
        /// 获取或设置音频采样率（仅音频流有效）。
        /// </summary>
        public int? SampleRate { get; set; }

        /// <summary>
        /// 获取或设置音频声道数（仅音频流有效）。
        /// </summary>
        public int? Channels { get; set; }

        /// <summary>
        /// 获取或设置音频采样格式（仅音频流有效）。
        /// </summary>
        public string? SampleFmt { get; set; }

        /// <summary>
        /// 获取或设置时长（单位：秒）。
        /// </summary>
        public double? Duration { get; set; }

        /// <summary>
        /// 获取或设置附加的元数据标签。
        /// </summary>
        public Dictionary<string, string> Tags { get; set; } = new Dictionary<string, string>();

        /// <summary>
        /// 获取或设置原始 probe 输出 JSON。
        /// </summary>
        public string? RawJson { get; set; }
    }
}