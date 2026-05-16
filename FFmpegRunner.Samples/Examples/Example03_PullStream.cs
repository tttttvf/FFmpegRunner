using FFmpegRunner;

namespace FFmpegRunner.Samples.Examples;

public static class Example03_PullStream
{
    public static void Run()
    {
        Console.WriteLine("=== 示例 3: 拉流保存 ===\n");

        // ─── 场景 A: 本地文件转码 ───
        Console.WriteLine("[场景 A] 本地文件转码");
        Console.Write("  输入文件（默认 input.mp4）: ");
        var input = Console.ReadLine();
        var sourceFile = string.IsNullOrWhiteSpace(input) ? "input.mp4" : input;

        Console.Write("  输出文件（默认 output.mp4）: ");
        var output = Console.ReadLine();
        var targetFile = string.IsNullOrWhiteSpace(output) ? "output.mp4" : output;

        var runnerFile = new FFmpegBuilder()
            .FromSource(sourceFile)
            .WithVideoCodec("libx264")
            .WithAudioCodec("aac")
            .WithCrf(23)
            .WithPreset("fast")
            .WithOverwrite(true)
            .ToFile(targetFile)
            .Build();

        Console.WriteLine($"  FFmpeg:   {runnerFile.FFmpegPath}");
        Console.WriteLine($"  输入源:   {runnerFile.SourcePath}");
        Console.WriteLine($"  输出:     {runnerFile.TargetPath}");
        Console.WriteLine($"  参数:     {runnerFile.CommandArguments}");
        Console.WriteLine($"  启动命令: ffmpeg {runnerFile.InputArguments} -i \"{runnerFile.SourcePath}\" {runnerFile.CommandArguments} \"{runnerFile.TargetPath}\"");
        Console.WriteLine();

        // ─── 场景 B: RTSP 拉流保存 ───
        Console.WriteLine("[场景 B] RTSP 拉流保存");
        Console.Write("  RTSP 地址（默认 rtsp://camera:554/stream）: ");
        var rtspInput = Console.ReadLine();
        var rtspUrl = string.IsNullOrWhiteSpace(rtspInput) ? "rtsp://camera:554/stream" : rtspInput;

        Console.Write("  输出文件（默认 recording.mp4）: ");
        var rtspOutput = Console.ReadLine();
        var rtspFile = string.IsNullOrWhiteSpace(rtspOutput) ? "recording.mp4" : rtspOutput;

        var runnerRtsp = new FFmpegBuilder()
            .FromRtspSource(rtspUrl, "tcp", opt => opt
                .WithBufferSize(819200)
                .WithTimeout(10_000_000))
            .WithVideoCodec("copy")
            .WithAudioCodec("copy")
            .WithOverwrite(true)
            .ToFile(rtspFile)
            .Build();

        Console.WriteLine($"  FFmpeg:   {runnerRtsp.FFmpegPath}");
        Console.WriteLine($"  输入源:   {runnerRtsp.SourcePath}");
        Console.WriteLine($"  输入参数: {runnerRtsp.InputArguments}");
        Console.WriteLine($"  输出:     {runnerRtsp.TargetPath}");
        Console.WriteLine($"  启动命令: ffmpeg {runnerRtsp.InputArguments} -i \"{runnerRtsp.SourcePath}\" {runnerRtsp.CommandArguments} \"{runnerRtsp.TargetPath}\"");
        Console.WriteLine();

        // ─── 场景 C: 带输入参数回调的拉流 ───
        Console.WriteLine("[场景 C] 带输入参数回调的拉流");
        var runnerCustom = new FFmpegBuilder()
            .FromSource("udp://239.0.0.1:1234", opt => opt
                .WithFormat("mpegts")
                .WithBufferSize(65536)
                .WithTimeout(5_000_000)
                .WithHardwareAcceleration(HardwareAccelerationType.Auto)
                .WithCustomArguments("-re"))
            .WithVideoCodec("libx264")
            .WithAudioCodec("aac")
            .WithOverwrite(true)
            .ToFile("output_udp.ts")
            .Build();

        Console.WriteLine($"  输入源:   {runnerCustom.SourcePath}");
        Console.WriteLine($"  输入参数: {runnerCustom.InputArguments}");
        Console.WriteLine($"  完整命令: ffmpeg {runnerCustom.InputArguments} -i \"{runnerCustom.SourcePath}\" {runnerCustom.CommandArguments} \"{runnerCustom.TargetPath}\"");
        Console.WriteLine();

        Console.WriteLine("提示: 取消注释 '.Start()' 即可实际启动 FFmpeg 进程");
        Console.WriteLine("注意: 建议始终配置 .WithOverwrite(true)，否则当输出文件已存在时");
        Console.WriteLine("      FFmpeg 会阻塞等待用户输入 'y/n'，导致管道无法正常获取数据流");
    }
}