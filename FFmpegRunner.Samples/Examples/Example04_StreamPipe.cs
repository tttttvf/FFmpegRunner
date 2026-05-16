using FFmpegRunner;

namespace FFmpegRunner.Samples.Examples;

public static class Example04_StreamPipe
{
    public static void Run()
    {
        Console.WriteLine("=== 示例 4: 流管道使用 ===\n");

        Console.WriteLine("流管道（StreamPipe）将 FFmpeg 输出的原始字节流直接透传给回调，");
        Console.WriteLine("不做任何帧解析或切分，适合需要自行处理原始数据的场景。\n");

        var runner = new FFmpegBuilder()
            .FromSource("input.mp4")
            .WithVideoCodec("h264")
            .WithAudioCodec("aac")
            .ToPipe(pipe => pipe
                .WithPipeType(PipeType.Stream)
                .WithBufferCapacity(100)
                .WithCallback((data, metadata) =>
                {
                    Console.WriteLine($"  收到流数据: {data.Length} 字节" +
                        $"{(metadata != null ? $", type={metadata.Type}" : "")}");
                }))
            .Build();

        Console.WriteLine($"  管道名称: {runner.Pipe?.PipeName}");
        Console.WriteLine($"  管道目标: {runner.Pipe?.GetOutputTarget()}");
        Console.WriteLine($"  完整命令: ffmpeg -i \"{runner.SourcePath}\" {runner.CommandArguments} {runner.Pipe?.GetOutputTarget()}");
        Console.WriteLine();

        Console.WriteLine("特点:");
        Console.WriteLine("  - 数据透传，无帧解析开销");
        Console.WriteLine("  - metadata 始终为 null");
        Console.WriteLine("  - 适合自定义协议或原始数据存储");
        Console.WriteLine();
        Console.WriteLine("提示: 取消注释 '.Start()' 即可启动");
        Console.WriteLine("注意: 建议始终配置 .WithOverwrite(true)，否则当管道目标已存在时");
        Console.WriteLine("      FFmpeg 会阻塞等待用户输入 'y/n'，导致管道无法正常获取数据流");
    }
}