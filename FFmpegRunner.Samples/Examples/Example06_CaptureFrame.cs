using FFmpegRunner;

namespace FFmpegRunner.Samples.Examples;

public static class Example06_CaptureFrame
{
    public static void Run()
    {
        Console.WriteLine("=== 示例 6: 管道捕获帧 ===\n");

        Console.WriteLine("FramePipe 使用 H.264 Annex B 起始码自动切分 NAL 单元，");
        Console.WriteLine("并按 Access Unit 边界聚合（SPS/PPS + Slice 合并为一帧），");
        Console.WriteLine("配合 IFrameAnalyzer 分析帧类型和关键帧信息。\n");

        Console.Write("请输入视频源（默认 input.mp4）: ");
        var input = Console.ReadLine();
        var source = string.IsNullOrWhiteSpace(input) ? "input.mp4" : input;

        var runner = new FFmpegBuilder()
            .FromSource(source)
            .WithVideoCodec("h264")
            .ToPipe(pipe => pipe
                .WithPipeType(PipeType.Frame)
                .WithBufferCapacity(50)
                .WithFrameAnalyzer(new H264FrameAnalyzer())
                .WithCallback((data, metadata) =>
                {
                    var typeStr = metadata?.Type switch
                    {
                        FrameType.I => "I帧",
                        FrameType.P => "P帧",
                        FrameType.B => "B帧",
                        FrameType.Audio => "音频",
                        _ => "未知"
                    };

                    Console.WriteLine(
                        $"  [{typeStr}] size={data.Length / 1024} KB, " +
                        $"keyFrame={metadata?.IsKeyFrame}, " +
                        $"pts={metadata?.Timestamp}");
                }))
            .Build();

        Console.WriteLine($"\n  FFmpeg:   {runner.FFmpegPath}");
        Console.WriteLine($"  输入源:   {runner.SourcePath}");
        Console.WriteLine($"  帧分析器: H264FrameAnalyzer");
        Console.WriteLine($"  完整命令: ffmpeg -i \"{runner.SourcePath}\" {runner.CommandArguments} {runner.Pipe?.GetOutputTarget()}");
        Console.WriteLine();

        Console.WriteLine("特点:");
        Console.WriteLine("  - 按 Access Unit 聚合，SPS/PPS/Slice 合并为一帧");
        Console.WriteLine("  - 可识别 I/P/B 帧类型和关键帧");
        Console.WriteLine("  - 支持 H264FrameAnalyzer / H265FrameAnalyzer / CompositeFrameAnalyzer");
        Console.WriteLine();
        Console.WriteLine("提示: 取消注释 '.Start()' 即可启动");
        Console.WriteLine("注意: 建议始终配置 .WithOverwrite(true)，否则当管道目标已存在时");
        Console.WriteLine("      FFmpeg 会阻塞等待用户输入 'y/n'，导致管道无法正常获取数据流");
    }
}