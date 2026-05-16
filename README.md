# FFmpegRunner

一个 .NET FFmpeg 包装库，提供 Fluent API 风格的链式调用接口，用于构建和执行 FFmpeg 命令。支持管道数据传输、帧级数据回调、RTSP/RTMP 推拉流等功能。

## 要求

- .NET Standard 2.0 或更高版本
- 系统须安装 FFmpeg（在 PATH 中或通过配置指定）

## 安装

通过 NuGet 包管理器安装：

```bash
dotnet add package FFmpegRunner
```

或通过 Package Manager Console：

```powershell
Install-Package FFmpegRunner
```

或直接在 `.csproj` 中添加：

```xml
<PackageReference Include="FFmpegRunner" Version="1.0.0" />
```

## 快速入门

```csharp
using FFmpegRunner;

var exitCode = new FFmpegBuilder()
    .FromSource("input.avi")
    .WithVideoCodec("libx264")
    .WithAudioCodec("aac")
    .WithCrf(23)
    .WithOverwrite(true)          // 覆盖已存在的文件，避免阻塞
    .ToFile("output.mp4")
    .Build()
    .Start();
```

> **重要**: 建议始终配置 `.WithOverwrite(true)`。当输出文件或管道目标已存在时，FFmpeg 会阻塞等待用户输入 `y/n`，导致程序无法正常获取数据流。

## 1. FFmpeg 配置

三种配置方式，优先级：`Builder.WithFFmpegPath()` > `FFmpegConfig.SetFFmpegPath()` > 自动探测

```csharp
// 全局配置
FFmpegConfig.SetFFmpegPath(@"C:\ffmpeg\bin\ffmpeg.exe");

// 每个 Builder 单独指定
new FFmpegBuilder()
    .WithFFmpegPath("/usr/local/bin/ffmpeg")
    ...

// 自动探测（默认）
// 按 PATH → 常见安装目录顺序查找
```

## 2. 视频信息加载

通过 ffprobe 查询媒体文件的流信息：

```csharp
var runner = new FFmpegBuilder()
    .FromSource("input.mp4")
    .ToFile("dummy.mp4")
    .Build();

var streams = runner.GetStreamInfo();

foreach (var s in streams)
{
    Console.WriteLine($"#{s.Index}: {s.CodecType} - {s.CodecName}");
    if (s.CodecType == "video")
        Console.WriteLine($"  {s.Width}x{s.Height}, {s.RFrameRate}");
}
```

## 3. 拉流保存

### 本地文件转码

```csharp
new FFmpegBuilder()
    .FromSource("input.avi")
    .WithVideoCodec("libx264")
    .WithAudioCodec("aac")
    .WithCrf(23)
    .WithPreset("fast")
    .WithOverwrite(true)
    .ToFile("output.mp4")
    .Build()
    .Start();
```

### RTSP 拉流录制

```csharp
new FFmpegBuilder()
    .FromRtspSource("rtsp://camera:554/stream", "tcp", opt => opt
        .WithBufferSize(819200)
        .WithTimeout(10_000_000))
    .WithVideoCodec("copy")
    .WithAudioCodec("copy")
    .WithOverwrite(true)
    .ToFile("recording.mp4")
    .Build()
    .Start();
```

### 带输入参数回调

```csharp
.FromSource("udp://239.0.0.1:1234", opt => opt
    .WithFormat("mpegts")
    .WithBufferSize(65536)
    .WithTimeout(5_000_000)
    .WithHardwareAcceleration(HardwareAccelerationType.Auto)
    .WithCustomArguments("-re"))
```

## 4. 流管道（StreamPipe）

原始字节流透传，不做任何帧解析，适合自定义协议或数据转发：

```csharp
new FFmpegBuilder()
    .FromSource("input.mp4")
    .WithVideoCodec("h264")
    .WithOverwrite(true)
    .ToPipe(pipe => pipe
        .WithPipeType(PipeType.Stream)
        .WithBufferCapacity(100)
        .WithCallback((data, metadata) =>
        {
            // data: 原始字节流
            Console.WriteLine($"收到 {data.Length} 字节");
        }))
    .Build()
    .Start();
```

## 5. 管道捕获图片（MJPEG → JPEG）

使用 MJPEG 编码，FFmpeg 输出的每个帧就是一张完整的 JPEG 图片，FramePipe 通过 SOI(0xFFD8)/EOI(0xFFD9) 标记自动切分，**无需额外解码**：

```csharp
new FFmpegBuilder()
    .FromSource("input.mp4")
    .WithVideoCodec("mjpeg")
    .WithCustomArguments("-q:v 5")
    .WithOverwrite(true)
    .ToPipe(pipe => pipe
        .WithPipeType(PipeType.Frame)
        .WithBufferCapacity(30)
        .WithCallback((data, metadata) =>
        {
            // data 就是完整 JPEG，直接保存
            File.WriteAllBytes($"frame_{DateTime.Now.Ticks}.jpg", data);
        }))
    .Build()
    .Start();
```

## 6. 管道捕获帧（H.264 AU 聚合 + 帧分析）

FramePipe 通过 Annex B 起始码自动切分 NAL 单元，并按 Access Unit 边界聚合（SPS/PPS + Slice 合并为一帧），配合 IFrameAnalyzer 分析帧类型：

```csharp
new FFmpegBuilder()
    .FromSource("input.mp4")
    .WithVideoCodec("h264")
    .WithOverwrite(true)
    .ToPipe(pipe => pipe
        .WithPipeType(PipeType.Frame)
        .WithBufferCapacity(50)
        .WithFrameAnalyzer(new H264FrameAnalyzer())
        .WithCallback((data, metadata) =>
        {
            Console.WriteLine(
                $"类型={metadata?.Type}, 关键帧={metadata?.IsKeyFrame}, " +
                $"大小={data.Length / 1024} KB");
        }))
    .Build()
    .Start();
```

支持的分析器：

| 分析器 | 说明 |
|--------|------|
| `H264FrameAnalyzer` | H.264 帧分析，识别 I/P/B 帧和关键帧 |
| `H265FrameAnalyzer` | H.265 帧分析，识别 IRAP 关键帧 |
| `MjpegFrameAnalyzer` | MJPEG 帧分析 |
| `CompositeFrameAnalyzer` | 组合多个分析器（默认） |

## 7. 推流使用

### RTMP 推流

```csharp
new FFmpegBuilder()
    .FromSource("input.mp4")
    .WithVideoCodec("libx264")
    .WithAudioCodec("aac")
    .WithCrf(23)
    .WithPreset("fast")
    .ToNetwork("rtmp://live.twitch.tv/app/streamkey")
    .Build()
    .Start();
```

### RTSP 推流

```csharp
new FFmpegBuilder()
    .FromSource("input.mp4")
    .WithVideoCodec("libx264")
    .WithAudioCodec("aac")
    .WithCrf(23)
    .ToRtsp("rtsp://server:554/live/stream", "tcp")
    .Build()
    .Start();
```

## 视频编码参数

| 方法 | 说明 |
|------|------|
| `WithVideoCodec(string)` | 视频编码器（默认 libx264） |
| `WithoutVideoCodec()` | 清除自定义编码 |
| `WithVideoBitrate(string)` | 比特率，如 "2M" |
| `WithCrf(int)` | CRF 质量（0-51） |
| `WithResolution(string)` | 分辨率，如 "1920x1080" |
| `WithFrameRate(int)` | 输出帧率 |
| `WithPixelFormat(string)` | 像素格式，如 "yuv420p" |
| `WithPreset(string)` | 编码预设 |

## 音频编码参数

| 方法 | 说明 |
|------|------|
| `WithAudioCodec(string)` | 音频编码器 |
| `WithAudioBitrate(string)` | 比特率，如 "128k" |
| `WithAudioSampleRate(int)` | 采样率 |
| `WithAudioChannels(int)` | 声道数 |

## 资源释放

```csharp
runner.Dispose();

// 或 using 语句
using var runner = new FFmpegBuilder()
    .FromSource("input.mp4")
    .ToFile("output.mp4")
    .Build();
```

## 示例项目

项目包含 `FFmpegRunner.Samples` 示例程序，涵盖以上 7 个使用场景：

```bash
dotnet run --project FFmpegRunner.Samples
```

运行后通过菜单选择要查看的示例。
