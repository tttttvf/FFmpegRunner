using FFmpegRunner;

namespace FFmpegRunner.Samples.Examples;

public static class Example07_PushStream
{
    public static void Run()
    {
        Console.WriteLine("=== 示例 7: 推流使用 ===\n");

        // ─── 场景 A: RTMP 推流 ───
        Console.WriteLine("[场景 A] RTMP 推流");
        Console.Write("  输入文件（默认 input.mp4）: ");
        var input = Console.ReadLine();
        var sourceFile = string.IsNullOrWhiteSpace(input) ? "input.mp4" : input;

        Console.Write("  RTMP 地址（默认 rtmp://live.twitch.tv/app/streamkey）: ");
        var rtmpInput = Console.ReadLine();
        var rtmpUrl = string.IsNullOrWhiteSpace(rtmpInput)
            ? "rtmp://live.twitch.tv/app/streamkey"
            : rtmpInput;

        var runnerRtmp = new FFmpegBuilder()
            .FromSource(sourceFile)
            .WithVideoCodec("libx264")
            .WithAudioCodec("aac")
            .WithCrf(23)
            .WithPreset("fast")
            .ToNetwork(rtmpUrl)
            .Build();

        Console.WriteLine($"  FFmpeg:   {runnerRtmp.FFmpegPath}");
        Console.WriteLine($"  输入源:   {runnerRtmp.SourcePath}");
        Console.WriteLine($"  输出:     {runnerRtmp.TargetPath}");
        Console.WriteLine($"  完整命令: ffmpeg -i \"{runnerRtmp.SourcePath}\" {runnerRtmp.CommandArguments} \"{runnerRtmp.TargetPath}\"");
        Console.WriteLine();

        // ─── 场景 B: RTSP 推流 ───
        Console.WriteLine("[场景 B] RTSP 推流");
        Console.Write("  RTSP 地址（默认 rtsp://server:554/live/stream）: ");
        var rtspInput = Console.ReadLine();
        var rtspUrl = string.IsNullOrWhiteSpace(rtspInput)
            ? "rtsp://server:554/live/stream"
            : rtspInput;

        var runnerRtsp = new FFmpegBuilder()
            .FromSource(sourceFile)
            .WithVideoCodec("libx264")
            .WithAudioCodec("aac")
            .WithCrf(23)
            .ToRtsp(rtspUrl, "tcp")
            .Build();

        Console.WriteLine($"  FFmpeg:   {runnerRtsp.FFmpegPath}");
        Console.WriteLine($"  输入源:   {runnerRtsp.SourcePath}");
        Console.WriteLine($"  输出:     {runnerRtsp.TargetPath}");
        Console.WriteLine($"  完整命令: ffmpeg -i \"{runnerRtsp.SourcePath}\" {runnerRtsp.CommandArguments} \"{runnerRtsp.TargetPath}\"");
        Console.WriteLine();

        // ─── 场景 C: 带输入参数回调的推流 ───
        Console.WriteLine("[场景 C] 带输入参数回调的推流");
        var runnerCustom = new FFmpegBuilder()
            .FromSource("rtsp://camera:554/stream", opt => opt
                .WithBufferSize(819200)
                .WithTimeout(10_000_000)
                .WithRtspTransport("tcp"))
            .WithVideoCodec("libx264")
            .WithAudioCodec("aac")
            .WithCrf(23)
            .WithPreset("ultrafast")
            .ToNetwork("rtmp://live.example.com/stream")
            .Build();

        Console.WriteLine($"  输入源:   {runnerCustom.SourcePath}");
        Console.WriteLine($"  输入参数: {runnerCustom.InputArguments}");
        Console.WriteLine($"  输出:     {runnerCustom.TargetPath}");
        Console.WriteLine($"  完整命令: ffmpeg {runnerCustom.InputArguments} -i \"{runnerCustom.SourcePath}\" {runnerCustom.CommandArguments} \"{runnerCustom.TargetPath}\"");
        Console.WriteLine();

        Console.WriteLine("提示: 取消注释 '.Start()' 即可启动推流");
    }
}