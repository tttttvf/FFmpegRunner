using FFmpegRunner;

namespace FFmpegRunner.Samples.Examples;

public static class Example01_FFmpegConfig
{
    public static void Run()
    {
        Console.WriteLine("=== 示例 1: FFmpeg 配置 ===\n");

        // 方式一：全局配置（影响所有 Builder 实例）
        Console.WriteLine("[方式一] 全局配置 FFmpegConfig.SetFFmpegPath()");
        try
        {
            FFmpegConfig.SetFFmpegPath(@"C:\ffmpeg\bin\ffmpeg.exe");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"  设置失败（可忽略）: {ex.Message}");
        }

        var globalPath = FFmpegConfig.GetFFmpegPath();
        Console.WriteLine($"  全局 FFmpeg 路径: {globalPath}");
        Console.WriteLine();

        // 方式二：每个 Builder 单独指定
        Console.WriteLine("[方式二] 每个 Builder 单独指定 WithFFmpegPath()");
        var runner = new FFmpegBuilder()
            .WithFFmpegPath(@"D:\tools\ffmpeg.exe")
            .FromSource("input.mp4")
            .ToFile("output.mp4")
            .Build();

        Console.WriteLine($"  当前 Builder 的 FFmpeg 路径: {runner.FFmpegPath}");
        Console.WriteLine();

        // 方式三：自动探测
        Console.WriteLine("[方式三] 自动探测（不传参时自动查找 PATH 和常见安装目录）");
        var runnerAuto = new FFmpegBuilder()
            .FromSource("input.mp4")
            .ToFile("output.mp4")
            .Build();

        Console.WriteLine($"  自动探测结果: {runnerAuto.FFmpegPath}");
        Console.WriteLine();

        Console.WriteLine("提示: 配置优先级 Builder.WithFFmpegPath() > FFmpegConfig.SetFFmpegPath() > 自动探测");
    }
}