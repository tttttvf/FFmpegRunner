using FFmpegRunner;

namespace FFmpegRunner.Samples.Examples;

public static class Example06_CaptureFrame
{
    public static void Run()
    {
        Console.WriteLine("=== 示例 6: 管道捕获帧 ===\n");

        // ─── 场景 A: 默认分割器 + 帧分析器 ───
        Console.WriteLine("[场景 A] 默认 CompositeFrameSplitter + H264FrameAnalyzer");
        Console.WriteLine("FramePipe 默认使用 CompositeFrameSplitter（H.264 → H.265 → MJPEG）");
        Console.WriteLine("按 Annex B 起始码切分 NAL 单元，再按 Access Unit 边界聚合。\n");

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
        Console.WriteLine($"  分割器:   CompositeFrameSplitter (默认)");
        Console.WriteLine($"  帧分析器: H264FrameAnalyzer");
        Console.WriteLine($"  完整命令: ffmpeg -i \"{runner.SourcePath}\" {runner.CommandArguments} {runner.Pipe?.GetOutputTarget()}");
        Console.WriteLine();
        Console.WriteLine("特点:");
        Console.WriteLine("  - 按 Access Unit 聚合，SPS/PPS/Slice 合并为一帧");
        Console.WriteLine("  - 可识别 I/P/B 帧类型和关键帧");
        Console.WriteLine("  - 支持 H264FrameAnalyzer / H265FrameAnalyzer / CompositeFrameAnalyzer");
        Console.WriteLine();

        // ─── 场景 B: 自定义 FrameSplitter ───
        Console.WriteLine("============================================================");
        Console.WriteLine("[场景 B] 自定义 FrameSplitter 链");
        Console.WriteLine("通过 WithFrameSplitter 可自定义分割器链，仅处理特定编码格式。\n");

        Console.Write("请输入视频源（默认 input.mp4）: ");
        var input2 = Console.ReadLine();
        var source2 = string.IsNullOrWhiteSpace(input2) ? "input.mp4" : input2;

        // 自定义分割器：只用 H.264 和 MJPEG，不包含 H.265
        var customSplitter = new CompositeFrameSplitter(
            new H264FrameSplitter(),
            new MjpegFrameSplitter()
        );

        var runner2 = new FFmpegBuilder()
            .FromSource(source2)
            .WithVideoCodec("h264")
            .WithOverwrite(true)
            .ToPipe(pipe => pipe
                .WithPipeType(PipeType.Frame)
                .WithBufferCapacity(50)
                .WithFrameSplitter(customSplitter)
                .WithFrameAnalyzer(new CompositeFrameAnalyzer(
                    new H264FrameAnalyzer(),
                    new MjpegFrameAnalyzer()))
                .WithCallback((data, metadata) =>
                {
                    Console.WriteLine(
                        $"  [type={metadata?.Type}] size={data.Length / 1024} KB, " +
                        $"keyFrame={metadata?.IsKeyFrame}");
                }))
            .Build();

        Console.WriteLine($"\n  FFmpeg:     {runner2.FFmpegPath}");
        Console.WriteLine($"  输入源:     {runner2.SourcePath}");
        Console.WriteLine($"  分割器:     H264FrameSplitter + MjpegFrameSplitter (自定义)");
        Console.WriteLine($"  帧分析器:   H264FrameAnalyzer + MjpegFrameAnalyzer");
        Console.WriteLine();
        Console.WriteLine("扩展方式:");
        Console.WriteLine("  // 实现自定义分割器");
        Console.WriteLine("  public class MyCodecSplitter : IFrameSplitter");
        Console.WriteLine("  {");
        Console.WriteLine("      public bool TryExtractFrame(List<byte> buffer, out byte[]? frame) { ... }");
        Console.WriteLine("      public bool TryFlush(out byte[]? frame) { ... }");
        Console.WriteLine("      public void Reset() { ... }");
        Console.WriteLine("  }");
        Console.WriteLine();
        Console.WriteLine("  // 注册到责任链");
        Console.WriteLine("  var splitter = new CompositeFrameSplitter();");
        Console.WriteLine("  splitter.AddSplitter(new MyCodecSplitter());");
        Console.WriteLine();
        Console.WriteLine("  // 或移除默认分割器");
        Console.WriteLine("  splitter.RemoveSplitter<H265FrameSplitter>();");
        Console.WriteLine();

        Console.WriteLine("提示: 取消注释 '.Start()' 即可启动");
        Console.WriteLine("注意: 建议始终配置 .WithOverwrite(true)，否则当管道目标已存在时");
        Console.WriteLine("      FFmpeg 会阻塞等待用户输入 'y/n'，导致管道无法正常获取数据流");
    }
}