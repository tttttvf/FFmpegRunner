# FFmpegRunner

一个现代化的 .NET FFmpeg 包装库，提供 Fluent API 风格的接口，用于构建和执行 FFmpeg 命令。支持管道数据传输、RTSP 流媒体处理、帧级数据回调等功能。

## 📦 功能特性

### 核心功能
- **Fluent API 设计模式** - 链式调用，代码简洁易读
- **FFmpeg 命令构建器** - 灵活配置音视频编码参数
- **管道数据传输** - 支持命名管道（Windows Named Pipes / Unix Domain Sockets）
- **RTSP 流媒体支持** - 完整的 RTSP 拉流和推流支持
- **帧级数据回调** - 支持流管道和帧管道两种模式
- **异步执行** - 支持同步和异步两种运行模式

### 编码和格式支持
- 多种视频编码器：H.264、H.265、MPEG4 等
- 多种音频编码器：AAC、MP3、Opus 等
- 丰富的视频参数：比特率、CRF、分辨率、帧率、像素格式等
- 多种容器格式：MP4、AVI、FLV、MKV 等

### RTSP 流媒体
- **RTSP 拉流** - 从 RTSP 源读取视频流
- **RTSP 推流** - 输出到 RTSP 服务器
- 支持 TCP/UDP 传输协议
- 智能参数优先级机制

## 🔧 系统要求

- .NET Standard 2.0 或更高版本
- FFmpeg 可执行文件（需在 PATH 中或通过配置指定）

### FFmpeg 安装

**Windows:**
```powershell
# 使用 winget
winget install FFmpeg.FFmpeg

# 或下载预编译版本
# https://www.gyan.dev/ffmpeg/builds/
```

**Linux (Ubuntu/Debian):**
```bash
sudo apt update
sudo apt install ffmpeg
```

**macOS:**
```bash
brew install ffmpeg
```

## 📥 安装

### NuGet 包管理器
```bash
dotnet add package FFmpegRunner
```

### 或在 csproj 中添加
```xml
<ItemGroup>
  <PackageReference Include="FFmpegRunner" Version="1.0.0" />
</ItemGroup>
```

## 🚀 快速开始

### 基本使用

```csharp
using FFmpegRunner;

// 简单的文件转码
var runner = new FFmpegBuilder()
    .FromSource("input.mp4")
    .WithVideoCodec("h264")
    .WithAudioCodec("aac")
    .WithVideoBitrate("2M")
    .ToFile("output.mp4")
    .Build();

int exitCode = runner.Start();
```

### 管道数据传输

```csharp
using FFmpegRunner;

// 使用管道接收数据
var runner = new FFmpegBuilder()
    .FromSource("input.mp4")
    .WithVideoCodec("h264")
    .ToPipe(pipe => pipe
        .WithCallback((data, metadata) =>
        {
            Console.WriteLine($"接收到 {data.Length} 字节的数据");
        })
        .WithBufferCapacity(200))
    .Build();

runner.Start();
```

### RTSP 拉流

```csharp
using FFmpegRunner;

// 从 RTSP 源拉取视频流
var runner = new FFmpegBuilder()
    .FromRtspSource("rtsp://192.168.1.100:554/stream", "tcp")
    .WithVideoCodec("copy")
    .ToFile("output.mp4")
    .Build();

runner.Start();
```

### RTSP 推流

```csharp
using FFmpegRunner;

// 推送到 RTSP 服务器
var runner = new FFmpegBuilder()
    .FromSource("input.mp4")
    .WithVideoCodec("h264")
    .WithAudioCodec("aac")
    .ToRtsp("rtsp://192.168.1.100:554/live/stream", "udp")
    .Build();

runner.Start();
```

## 📚 API 参考

### FFmpegBuilder

主要的参数配置方法：

#### 输入源配置

| 方法 | 说明 |
|------|------|
| `FromSource(string sourcePath)` | 设置媒体源文件路径 |
| `FromRtspSource(string rtspAddress, string transportProtocol = "tcp")` | 配置 RTSP 拉流源 |

#### 视频参数

| 方法 | 说明 |
|------|------|
| `WithVideoCodec(string codec)` | 设置视频编码器 |
| `WithVideoBitrate(string bitrate)` | 设置视频比特率（如 "2M"、"1500k"）|
| `WithCrf(int crf)` | 设置 CRF 质量参数（0-51）|
| `WithResolution(string resolution)` | 设置分辨率（如 "1920x1080"）|
| `WithFrameRate(int frameRate)` | 设置帧率 |
| `WithPixelFormat(string pixFmt)` | 设置像素格式（如 "yuv420p"）|
| `WithPreset(string preset)` | 设置编码预设（如 "fast"、"slow"）|

#### 音频参数

| 方法 | 说明 |
|------|------|
| `WithAudioCodec(string codec)` | 设置音频编码器 |
| `WithAudioBitrate(string bitrate)` | 设置音频比特率（如 "128k"）|
| `WithAudioSampleRate(int sampleRate)` | 设置采样率（如 44100、48000）|
| `WithAudioChannels(int channels)` | 设置声道数（1=单声道，2=立体声）|

#### 输出配置

| 方法 | 说明 |
|------|------|
| `ToFile(string filePath)` | 输出到文件 |
| `ToPipe(string? pipeName = null)` | 输出到管道 |
| `ToNetwork(string networkUrl)` | 输出到网络地址（如 RTMP）|
| `ToRtsp(string rtspAddress, string transportProtocol = "tcp")` | 输出到 RTSP 推流 |

#### 格式和其他参数

| 方法 | 说明 |
|------|------|
| `WithFormat(string format)` | 设置输出格式 |
| `WithOverwrite(bool overwrite = true)` | 设置是否覆盖输出文件（-y）|
| `WithNetworkTimeout(long timeoutMicroseconds)` | 设置网络超时（微秒）|
| `WithBufferSize(int bufferSize)` | 设置网络缓冲区大小 |
| `WithThreads(int threadCount)` | 设置线程数 |
| `WithCustomArguments(string arguments)` | 添加自定义 FFmpeg 参数 |

### RTSP 参数说明

#### FromRtspSource

```csharp
// 签名
public FFmpegBuilder FromRtspSource(string rtspAddress, string transportProtocol = "tcp")

// 参数
// - rtspAddress: RTSP 拉流地址（必填）
// - transportProtocol: 传输协议（可选，默认 "tcp"）
//   支持: "tcp", "udp", "http"

// 示例
.FromRtspSource("rtsp://camera.local:554/stream", "tcp")
```

#### ToRtsp

```csharp
// 签名
public FFmpegBuilder ToRtsp(string rtspAddress, string transportProtocol = "tcp")

// 参数
// - rtspAddress: RTSP 推流地址（必填）
// - transportProtocol: 传输协议（可选，默认 "tcp"）
//   支持: "tcp", "udp"

// 重要说明
// 此方法会自动添加 -rtsp_transport 和 -f rtsp 参数
// 如果之前调用了 WithFormat()，会自动覆盖为 -f rtsp

// 示例
.ToRtsp("rtsp://192.168.1.100:554/live/stream", "udp")
```

### 管道配置 (PipeTarget)

```csharp
// 配置回调
ToPipe(pipe => pipe
    .WithCallback((data, metadata) =>
    {
        // data: 接收到的字节数组
        // metadata: 帧元数据（可为 null）
        Console.WriteLine($"数据: {data.Length} 字节");
    })
    .WithBufferCapacity(100)  // 缓冲区容量
    .WithPipeType(PipeType.Frame)  // 管道类型: Frame 或 Stream
    .WithPipeName("custom-pipe"))  // 自定义管道名称
```

### FFmpegRunner

```csharp
// 同步执行
var runner = new FFmpegBuilder()
    .FromSource("input.mp4")
    .ToFile("output.mp4")
    .Build();

int exitCode = runner.Start();

// 异步执行
CancellationTokenSource cts = new CancellationTokenSource();
int exitCode = await runner.StartAsync(cts.Token);

// 设置超时（毫秒）
runner.TimeoutMilliseconds = 30000;

// 获取媒体信息
List<StreamInfo> streams = runner.GetStreamInfo();

// 清理资源
runner.Dispose();
```

## 💡 使用示例

### 示例 1：视频转码

```csharp
var runner = new FFmpegBuilder()
    .FromSource("input.avi")
    .WithVideoCodec("libx264")
    .WithAudioCodec("aac")
    .WithVideoBitrate("2M")
    .WithAudioBitrate("128k")
    .WithCrf(23)
    .WithPreset("medium")
    .ToFile("output.mp4")
    .Build();

runner.Start();
```

### 示例 2：视频缩放和格式转换

```csharp
var runner = new FFmpegBuilder()
    .FromSource("input.mkv")
    .WithVideoCodec("h264")
    .WithAudioCodec("aac")
    .WithResolution("1280x720")
    .WithPixelFormat("yuv420p")
    .WithFormat("mp4")
    .WithOverwrite(true)
    .ToFile("output.mp4")
    .Build();
```

### 示例 3：RTSP 拉流录制

```csharp
var runner = new FFmpegBuilder()
    .FromRtspSource("rtsp://camera.local:554/h264/main/av_stream", "tcp")
    .WithNetworkTimeout(5000000)  // 5秒超时
    .WithBufferSize(819200)        // 800KB 缓冲区
    .WithVideoCodec("copy")
    .WithAudioCodec("copy")
    .ToFile($"recording_{DateTime.Now:yyyyMMdd_HHmmss}.mp4")
    .Build();

runner.Start();
```

### 示例 4：直播推流

```csharp
var runner = new FFmpegBuilder()
    .FromSource("live_source.mp4")
    .WithVideoCodec("libx264")
    .WithAudioCodec("aac")
    .WithVideoBitrate("2500k")
    .WithAudioBitrate("128k")
    .WithPreset("veryfast")
    .WithFormat("flv")
    .ToNetwork("rtmp://live.example.com/app/stream_key")
    .Build();

runner.Start();
```

### 示例 5：帧级数据处理（管道模式）

```csharp
// 使用帧管道接收完整帧
var runner = new FFmpegBuilder()
    .FromSource("input.mp4")
    .WithVideoCodec("h264")
    .ToPipe(pipe => pipe
        .WithPipeType(PipeType.Frame)  // 使用帧管道
        .WithCallback((data, metadata) =>
        {
            if (metadata != null)
            {
                Console.WriteLine($"帧类型: {metadata.Type}, " +
                                $"大小: {metadata.Size}, " +
                                $"时间戳: {metadata.Timestamp}");
            }
        }))
    .Build();

runner.Start();
```

### 示例 6：批量处理

```csharp
string[] inputFiles = Directory.GetFiles("input", "*.avi");

foreach (var input in inputFiles)
{
    var output = Path.Combine("output", Path.GetFileNameWithoutExtension(input) + ".mp4");

    var runner = new FFmpegBuilder()
        .FromSource(input)
        .WithVideoCodec("h264")
        .WithAudioCodec("aac")
        .WithOverwrite(true)
        .ToFile(output)
        .Build();

    int result = runner.Start();
    Console.WriteLine($"{input} -> {output}: {(result == 0 ? "成功" : "失败")}");
}
```

## 🔍 生成的 FFmpeg 命令示例

FFmpegBuilder 会生成标准的 FFmpeg 命令行。以下是一些生成的命令示例：

### 基本转码
```
ffmpeg -y -i input.mp4 -c:v h264 -c:a aac -b:v 2M -b:a 128k output.mp4
```

### RTSP 拉流
```
ffmpeg -y -rtsp_transport tcp rtsp://192.168.1.100:554/stream output.mp4
```

### RTSP 推流
```
ffmpeg -y -i input.mp4 -c:v h264 -c:a aac -rtsp_transport udp -f rtsp rtsp://192.168.1.100:554/live/stream
```

### 管道输出
```
ffmpeg -y -i input.mp4 -c:v h264 -f h264 \\.\pipe\ffmpeg-pipe-abc123
```

## ⚠️ 注意事项

1. **FFmpeg 路径**: 确保 FFmpeg 可执行文件在系统 PATH 中，或通过 `FFmpegConfig.SetFFmpegPath()` 设置

2. **RTSP 参数优先级**: 使用 `ToRtsp()` 方法会自动覆盖之前设置的格式参数，始终使用 `-f rtsp`

3. **资源释放**: 使用完 FFmpegRunner 后，请调用 `Dispose()` 方法释放资源，或使用 `using` 语句

4. **超时设置**: 对于网络流，建议设置合理的超时时间，避免长时间阻塞

5. **管道模式**: 在 Windows 上使用管道模式需要管理员权限

## 🐛 故障排除

### FFmpeg 未找到

```csharp
// 方式 1: 通过 FFmpegConfig 设置
FFmpegConfig.SetFFmpegPath("/path/to/ffmpeg");

// 方式 2: 通过 Builder 设置
var runner = new FFmpegBuilder()
    .WithFFmpegPath("/path/to/ffmpeg")
    .FromSource("input.mp4")
    .ToFile("output.mp4")
    .Build();
```

### RTSP 连接失败

```csharp
// 增加超时时间和缓冲区大小
var runner = new FFmpegBuilder()
    .FromRtspSource("rtsp://camera.local:554/stream", "tcp")
    .WithNetworkTimeout(10000000)  // 10秒超时
    .WithBufferSize(1024000)       // 1MB 缓冲区
    .ToFile("output.mp4")
    .Build();
```

### 管道权限问题（Windows）

确保以管理员身份运行应用程序，或使用非管理员用户可访问的管道名称。

## 📄 许可证

本项目采用 MIT 许可证 - 详见 [LICENSE](LICENSE) 文件

## 🤝 贡献

欢迎提交 Issue 和 Pull Request！

## 📧 联系方式

如有问题或建议，请通过以下方式联系：
- 提交 GitHub Issue
- 发送邮件至项目维护者

---

**享受 FFmpegRunner 带来的便捷开发体验！** 🎉
