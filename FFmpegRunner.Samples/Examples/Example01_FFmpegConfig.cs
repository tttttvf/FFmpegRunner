using FFmpegRunner;

namespace FFmpegRunner.Samples.Examples;

public static class Example01_FFmpegConfig
{
    public static void Run()
    {
        Console.WriteLine("=== 示例 1: FFmpeg 配置 ===\n");

        // 方式一：全局配置（影响所有 Builder 实例）
        Console.WriteLine("[方式一] 全局配置 FFmpegConfig.SetFFmpegPath()");
        FFmpegConfig.SetFFmpegPath(@"C:\ffmpeg\bin\ffmpeg.exe");

        try
        {
            var globalPath = FFmpegConfig.GetFFmpegPath();
            Console.WriteLine($"  全局 FFmpeg 路径: {globalPath}");
        }
        catch (FFmpegRunnerException ex)
        {
            Console.WriteLine($"  路径无效 (ErrorCode={ex.ErrorCode}): {ex.Message}");
        }
        Console.WriteLine();

        // 方式二：每个 Builder 单独指定
        Console.WriteLine("[方式二] 每个 Builder 单独指定 WithFFmpegPath()");
        Console.WriteLine("  配置优先级: Builder.WithFFmpegPath() > FFmpegConfig.SetFFmpegPath() > 自动探测");
        Console.WriteLine();

        var runner = new FFmpegBuilder()
            .WithFFmpegPath(@"D:\tools\ffmpeg.exe")
            .FromSource("input.mp4")
            .ToFile("output.mp4")
            .Build();

        Console.WriteLine($"  当前 Builder 的 FFmpeg 路径: {runner.FFmpegPath}");
        Console.WriteLine();

        // 方式三：自动探测
        Console.WriteLine("[方式三] 自动探测（不传参时自动查找 PATH 和常见安装目录）");
        Console.WriteLine("  当 FFmpeg 未安装时，将抛出 FFmpegRunnerException (ErrorCode=FFMPEG_NOT_FOUND)");
        Console.WriteLine();

        var runnerAuto = new FFmpegBuilder()
            .FromSource("input.mp4")
            .ToFile("output.mp4")
            .Build();

        Console.WriteLine($"  自动探测结果: {runnerAuto.FFmpegPath}");
        Console.WriteLine();

        Console.WriteLine("错误处理示例:");
        Console.WriteLine("  try");
        Console.WriteLine("  {");
        Console.WriteLine("      var path = FFmpegConfig.GetFFmpegPath();");
        Console.WriteLine("  }");
        Console.WriteLine("  catch (FFmpegRunnerException ex) when (ex.ErrorCode == \"FFMPEG_NOT_FOUND\")");
        Console.WriteLine("  {");
        Console.WriteLine("      Console.WriteLine($\"FFmpeg 未找到: {ex.Message}\");");
        Console.WriteLine("  }");
    }
}