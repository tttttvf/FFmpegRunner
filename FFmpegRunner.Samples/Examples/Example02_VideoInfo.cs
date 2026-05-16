using FFmpegRunner;

namespace FFmpegRunner.Samples.Examples;

public static class Example02_VideoInfo
{
    public static void Run()
    {
        Console.WriteLine("=== 示例 2: 视频信息加载 ===\n");

        Console.Write("请输入视频文件路径（直接回车使用默认值 input.mp4）: ");
        var input = Console.ReadLine();
        var filePath = string.IsNullOrWhiteSpace(input) ? "input.mp4" : input;

        var runner = new FFmpegBuilder()
            .FromSource(filePath)
            .ToFile("dummy.mp4")
            .Build();

        Console.WriteLine($"\n正在通过 ffprobe 查询: {filePath}");
        Console.WriteLine("（需要系统已安装 ffprobe）\n");

        try
        {
            var streams = runner.GetStreamInfo();

            if (streams.Count == 0)
            {
                Console.WriteLine("  未检测到媒体流信息");
                return;
            }

            Console.WriteLine($"共检测到 {streams.Count} 个媒体流:\n");

            for (int i = 0; i < streams.Count; i++)
            {
                var s = streams[i];
                Console.WriteLine($"  ─── 流 #{i} ───");
                Console.WriteLine($"  类型:     {s.CodecType}");
                Console.WriteLine($"  编码:     {s.CodecName}");

                if (s.CodecType == "video")
                {
                    Console.WriteLine($"  分辨率:   {s.Width} x {s.Height}");
                    Console.WriteLine($"  像素格式: {s.PixFmt}");
                    Console.WriteLine($"  帧率:     {s.RFrameRate}");
                }
                else if (s.CodecType == "audio")
                {
                    Console.WriteLine($"  采样率:   {s.SampleRate} Hz");
                    Console.WriteLine($"  声道数:   {s.Channels}");
                    Console.WriteLine($"  采样格式: {s.SampleFmt}");
                }

                if (s.BitRate > 0)
                    Console.WriteLine($"  比特率:   {s.BitRate} bps");

                if (s.Duration.HasValue)
                    Console.WriteLine($"  时长:     {s.Duration.Value} s");

                Console.WriteLine();
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"  查询失败: {ex.Message}");
            Console.WriteLine("  请确认 ffprobe 已安装且在 PATH 中");
        }
    }
}