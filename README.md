# FFmpegRunner

一个现代化的 .NET FFmpeg 包装库，提供 Fluent API 风格的链式调用接口，用于构建和执行 FFmpeg 命令。支持管道数据传输、RTSP 流媒体处理、帧级数据回调等功能。

## 要求

- .NET Standard 2.0 或更高版本
- 系统须安装 FFmpeg（在 PATH 中或通过配置指定）

## 快速入门

### 文件转码

```csharp
using FFmpegRunner;

var runner = new FFmpegBuilder()
    .FromSource("input.avi")
    .WithVideoCodec("libx264")
    .WithAudioCodec("aac")
    .WithVideoBitrate("2M")
    .WithCrf(23)
    .ToFile("output.mp4")
    .Build();

int exitCode = runner.Start();
```

### 异步执行

```csharp
var exitCode = await runner.StartAsync(cancellationToken);
```

## 输入源配置

### 本地文件

```C++
// 简单方式
.FromSource("input.mp4")

// 带输入参数配置
.FromSource("input.mp4", opt => opt
    .WithFrameRate(30)
    .WithHardwareAcceleration(HardwareAccelerationType.D3d11va)
    .WithSeekPosition("00:01:30")
    .WithDuration("00:00:10"))
```

### RTSP 拉流

```csharp
// 简单方式
.FromRtspSource("rtsp://192.168.1.100:554/stream")

// 指定传输协议（tcp/udp/http）
.FromRtspSource("rtsp://192.168.1.100:554/stream", "tcp")

// 带输入参数配置
.FromRtspSource("rtsp://192.168.1.100:554/stream", "tcp", opt => opt
    .WithBufferSize(65536)
    .WithTimeout(5000000))
```

### 输入参数选项

| 方法                                                   | FFmpeg 参数      | 说明        |
| ---------------------------------------------------- | -------------- | --------- |
| `WithFrameRate(int)`                                 | `-r`           | 输入帧率      |
| `WithVideoCodec(string)`                             | `-c:v`         | 输入视频解码器   |
| `WithAudioCodec(string)`                             | `-c:a`         | 输入音频解码器   |
| `WithBufferSize(int)`                                | `-buffer_size` | 输入缓冲区大小   |
| `WithTimeout(long)`                                  | `-timeout`     | 输入超时（微秒）  |
| `WithFormat(string)`                                 | `-f`           | 强制输入格式    |
| `WithHardwareAcceleration(HardwareAccelerationType)` | `-hwaccel`     | 硬件加速      |
| `WithHardwareAcceleration(string)`                   | `-hwaccel`     | 硬件加速（自定义） |
| `WithNoVideo()`                                      | `-vn`          | 禁用视频      |
| `WithNoAudio()`                                      | `-an`          | 禁用音频      |
| `WithSeekPosition(string)`                           | `-ss`          | 跳转到指定时间   |
| `WithDuration(string)`                               | `-t`           | 限制读取时长    |

## 输出配置

### 文件输出

```csharp
.ToFile("output.mp4")
```

### RTSP 推流

```csharp
.ToRtsp("rtsp://192.168.1.100:554/live/stream")
.ToRtsp("rtsp://192.168.1.100:554/live/stream", "udp")
```

### RTMP 推流

```csharp
.ToNetwork("rtmp://live.example.com/app/stream_key")
```

## 管道模式

支持通过命名管道接收 FFmpeg 输出的流数据或帧数据。

### 流管道（StreamPipe）

直接接收原始字节流，适合文件写入或转发。

```csharp
new FFmpegBuilder()
    .FromSource("input.mp4")
    .WithVideoCodec("h264")
    .ToPipe(pipe => pipe
        .WithCallback((data, metadata) =>
        {
            // data: 原始字节数据
        })
        .WithBufferCapacity(200))
    .Build();
```

### 帧管道（FramePipe）

按帧协议解析，每次回调返回一帧完整的帧数据。协议格式为 `(4字节长度 BigEndian) + (帧数据)`。

```csharp
new FFmpegBuilder()
    .FromSource("input.mp4")
    .WithVideoCodec("h264")
    .ToPipe(pipe => pipe
        .WithPipeType(PipeType.Frame)
        .WithCallback((data, metadata) =>
        {
            Console.WriteLine($"帧大小: {metadata.Size}, 类型: {metadata.Type}, 关键帧: {metadata.IsKeyFrame}");
        }))
    .Build();
```

### 帧分析

帧管道默认使用内置的帧分析器（CompositeFrameAnalyzer），支持 H.264、H.265 和 MJPEG 帧识别。

```csharp
// 自定义帧分析器
pipe.WithFrameAnalyzer(new CompositeFrameAnalyzer(
    new H264FrameAnalyzer(),
    new H265FrameAnalyzer(),
    new MjpegFrameAnalyzer()));

// 禁用帧分析
pipe.WithFrameAnalyzer(null);
```

## 视频编码参数

| 方法                         | 说明                   |
| -------------------------- | -------------------- |
| `WithVideoCodec(string)`   | 视频编码器（默认 libx264）    |
| `WithoutVideoCodec()`      | 清除自定义编码，恢复默认         |
| `WithVideoBitrate(string)` | 视频比特率，如 "2M"、"1500k" |
| `WithCrf(int)`             | CRF 质量（0-51）         |
| `WithResolution(string)`   | 分辨率，如 "1920x1080"    |
| `WithFrameRate(int)`       | 输出帧率                 |
| `WithPixelFormat(string)`  | 像素格式，如 "yuv420p"     |
| `WithPreset(string)`       | 编码预设，如 "fast"、"slow" |

## 音频编码参数

| 方法                         | 说明                |
| -------------------------- | ----------------- |
| `WithAudioCodec(string)`   | 音频编码器             |
| `WithAudioBitrate(string)` | 音频比特率，如 "128k"    |
| `WithAudioSampleRate(int)` | 采样率，如 44100、48000 |
| `WithAudioChannels(int)`   | 声道数               |

## FFmpeg 路径配置

```csharp
// 全局配置
FFmpegConfig.SetFFmpegPath(@"C:\ffmpeg\bin\ffmpeg.exe");

// 或 Builder 级别
new FFmpegBuilder()
    .WithFFmpegPath("/usr/local/bin/ffmpeg")
    ...
```

## 资源释放

```csharp
runner.Dispose();

// 或 using 语句
using var runner = new FFmpegBuilder()
    .FromSource("input.mp4")
    .ToFile("output.mp4")
    .Build();
```

## 示例

### RTSP 录制

```csharp
new FFmpegBuilder()
    .FromRtspSource("rtsp://camera.local:554/stream", "tcp", opt => opt
        .WithTimeout(10000000)
        .WithBufferSize(819200))
    .WithVideoCodec("copy")
    .WithAudioCodec("copy")
    .ToFile("recording.mp4")
    .Build()
    .Start();
```

### 帧管道实时处理

```csharp
new FFmpegBuilder()
    .FromSource("input.mp4")
    .WithVideoCodec("h264")
    .ToPipe(pipe => pipe
        .WithPipeType(PipeType.Frame)
        .WithCallback((data, metadata) =>
        {
            if (metadata.IsKeyFrame)
            {
                // 保存关键帧（I 帧）
                File.WriteAllBytes($"keyframe_{DateTime.Now.Ticks}.h264", data);
            }
        }))
    .Build()
    .Start();
```

