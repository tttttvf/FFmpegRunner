﻿using FFmpegRunner.Samples.Examples;

Console.WriteLine("╔══════════════════════════════════════════╗");
Console.WriteLine("║     FFmpegRunner.Samples - 示例程序      ║");
Console.WriteLine("╚══════════════════════════════════════════╝");
Console.WriteLine();

var examples = new Dictionary<string, Action>
{
    { "1",  () => Example01_FFmpegConfig.Run()     },
    { "2",  () => Example02_VideoInfo.Run()         },
    { "3",  () => Example03_PullStream.Run()        },
    { "4",  () => Example04_StreamPipe.Run()        },
    { "5",  () => Example05_CaptureImage.Run()      },
    { "6",  () => Example06_CaptureFrame.Run()      },
    { "7",  () => Example07_PushStream.Run()        },
};

while (true)
{
    Console.WriteLine("请选择要运行的示例（输入编号，输入 q 退出）:\n");
    Console.WriteLine("  ┌─────┬──────────────────────────────────────┐");
    Console.WriteLine("  │ 编号 │ 示例说明                             │");
    Console.WriteLine("  ├─────┼──────────────────────────────────────┤");
    Console.WriteLine("  │  1  │ FFmpeg 配置（全局/局部/自动探测）     │");
    Console.WriteLine("  │  2  │ 视频信息加载（ffprobe 查询流信息）     │");
    Console.WriteLine("  │  3  │ 拉流保存（文件转码 / RTSP 录制）      │");
    Console.WriteLine("  │  4  │ 流管道使用（StreamPipe 原始数据透传）  │");
    Console.WriteLine("  │  5  │ 管道捕获图片（MJPEG → 保存为 JPEG）   │");
    Console.WriteLine("  │  6  │ 管道捕获帧（H.264 AU 聚合 + 帧分析）  │");
    Console.WriteLine("  │  7  │ 推流使用（RTMP / RTSP 推流）          │");
    Console.WriteLine("  └─────┴──────────────────────────────────────┘");
    Console.WriteLine();
    Console.Write(">>> ");

    var input = Console.ReadLine()?.Trim().ToLower();

    if (string.IsNullOrEmpty(input))
        continue;

    if (input == "q" || input == "quit" || input == "exit")
        break;

    if (examples.TryGetValue(input ?? "", out var runExample))
    {
        Console.Clear();
        runExample();
    }
    else
    {
        Console.WriteLine("无效输入，请重新选择。\n");
        continue;
    }

    Console.WriteLine();
    Console.WriteLine("按任意键返回菜单...");
    Console.ReadKey();
    Console.Clear();
}