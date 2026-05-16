using FFmpegRunner;

namespace FFmpegRunner.Samples.Examples;

public static class Example05_CaptureImage
{
    public static void Run()
    {
        Console.WriteLine("=== 示例 5: 管道捕获图片 ===\n");

        Console.WriteLine("通过 MJPEG 编码，FFmpeg 输出的每个帧就是一张完整的 JPEG 图片，");
        Console.WriteLine("FramePipe 通过 SOI(0xFFD8)/EOI(0xFFD9) 标记自动切分，无需额外解码。\n");

        Console.Write("请输入视频源（默认 input.mp4）: ");
        var input = Console.ReadLine();
        var source = string.IsNullOrWhiteSpace(input) ? "input.mp4" : input;

        Console.Write("图片输出目录（默认 ./captured_images）: ");
        var dirInput = Console.ReadLine();
        var outputDir = string.IsNullOrWhiteSpace(dirInput) ? "./captured_images" : dirInput;

        Directory.CreateDirectory(outputDir);

        var frameCount = 0;
        var saveLock = new object();

        var runner = new FFmpegBuilder()
            .FromSource(source)
            .WithVideoCodec("mjpeg")
            .WithCustomArguments("-q:v 5")
            .ToPipe(pipe => pipe
                .WithPipeType(PipeType.Frame)
                .WithBufferCapacity(30)
                .WithCallback((data, metadata) =>
                {
                    Interlocked.Increment(ref frameCount);
                    var fileName = $"frame_{metadata?.Timestamp ?? 0}.jpg";
                    var filePath = Path.Combine(outputDir, fileName);

                    lock (saveLock)
                    {
                        File.WriteAllBytes(filePath, data);
                    }

                    Console.WriteLine($"  [{frameCount}] 已保存: {fileName}  " +
                        $"({data.Length / 1024} KB, keyFrame={metadata?.IsKeyFrame})");
                }))
            .Build();

        Console.WriteLine($"\n  FFmpeg:   {runner.FFmpegPath}");
        Console.WriteLine($"  输入源:   {runner.SourcePath}");
        Console.WriteLine($"  输出目录: {Path.GetFullPath(outputDir)}");
        Console.WriteLine($"  完整命令: ffmpeg -i \"{runner.SourcePath}\" {runner.CommandArguments} {runner.Pipe?.GetOutputTarget()}");
        Console.WriteLine();

        Console.WriteLine("特点:");
        Console.WriteLine("  - 每个帧是完整 JPEG，可直接用 Image.Load() 加载编辑");
        Console.WriteLine("  - 无需 H.264 解码器，零额外依赖");
        Console.WriteLine("  - 适合实时预览、AI 分析、截图保存");
        Console.WriteLine();
        Console.WriteLine("提示: 取消注释 '.Start()' 即可启动");
        Console.WriteLine("注意: 建议始终配置 .WithOverwrite(true)，否则当管道目标已存在时");
        Console.WriteLine("      FFmpeg 会阻塞等待用户输入 'y/n'，导致管道无法正常获取数据流");
    }
}