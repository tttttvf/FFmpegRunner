# FFmpegRunner

A .NET FFmpeg wrapper library providing a Fluent API-style chained interface for building and executing FFmpeg commands. Supports pipe data transfer, frame-level data callbacks, RTSP/RTMP streaming and more.

## Installation

```bash
dotnet add package FFmpegRunner
```

## Requirements

- .NET Standard 2.0 or .NET 8.0+
- FFmpeg must be installed on the system (in PATH or configured manually)

## Quick Start

```csharp
using FFmpegRunner;

var exitCode = new FFmpegBuilder()
    .FromSource("input.avi")
    .WithVideoCodec("libx264")
    .WithAudioCodec("aac")
    .WithCrf(23)
    .WithOverwrite(true)
    .ToFile("output.mp4")
    .Build()
    .Start();
```

> It is recommended to always use `.WithOverwrite(true)` to prevent FFmpeg from blocking on user input when the output file already exists.

## Features

### FFmpeg Configuration

Three ways to configure, priority: `Builder.WithFFmpegPath()` > `FFmpegConfig.SetFFmpegPath()` > auto detection

```csharp
// Global configuration
FFmpegConfig.SetFFmpegPath(@"C:\ffmpeg\bin\ffmpeg.exe");

// Per-builder configuration
new FFmpegBuilder().WithFFmpegPath("/usr/local/bin/ffmpeg");

// Auto detection (default)
// Searches PATH → common installation directories
```

### Media Information

Query media stream information via ffprobe:

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

### StreamPipe

Raw byte stream passthrough without frame parsing, suitable for custom protocols or data forwarding:

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
            Console.WriteLine($"Received {data.Length} bytes");
        }))
    .Build()
    .Start();
```

### FramePipe

Supports H.264/H.265 Annex B start code parsing, Access Unit aggregation, and frame type analysis. Use with IFrameAnalyzer to identify frame types:

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
                $"Type={metadata?.Type}, KeyFrame={metadata?.IsKeyFrame}, " +
                $"Size={data.Length / 1024} KB");
        }))
    .Build()
    .Start();
```

Supported analyzers:

| Analyzer | Description |
|----------|-------------|
| `H264FrameAnalyzer` | H.264 frame analysis, identifies I/P/B frames and keyframes |
| `H265FrameAnalyzer` | H.265 frame analysis, identifies IRAP keyframes |
| `MjpegFrameAnalyzer` | MJPEG frame analysis, splits by SOI/EOI markers |
| `CompositeFrameAnalyzer` | Combines multiple analyzers (default) |

### MJPEG Image Capture

With MJPEG encoding, each frame is a complete JPEG image - no decoding needed:

```csharp
new FFmpegBuilder()
    .FromSource("input.mp4")
    .WithVideoCodec("mjpeg")
    .WithCustomArguments("-q:v 5")
    .WithOverwrite(true)
    .ToPipe(pipe => pipe
        .WithPipeType(PipeType.Frame)
        .WithCallback((data, metadata) =>
        {
            File.WriteAllBytes($"frame_{DateTime.Now.Ticks}.jpg", data);
        }))
    .Build()
    .Start();
```

### Streaming

Supports RTMP/RTSP streaming:

```csharp
// RTMP
new FFmpegBuilder()
    .FromSource("input.mp4")
    .WithVideoCodec("libx264")
    .WithAudioCodec("aac")
    .WithCrf(23)
    .ToNetwork("rtmp://live.example.com/app/streamkey")
    .Build()
    .Start();

// RTSP
new FFmpegBuilder()
    .FromSource("input.mp4")
    .WithVideoCodec("libx264")
    .WithAudioCodec("aac")
    .ToRtsp("rtsp://server:554/live/stream", "tcp")
    .Build()
    .Start();
```

### Hardware Acceleration

Supports hardware acceleration via `InputOptions.WithHardwareAcceleration()`:

- `Auto`, `Vdpau`, `Dxva2`, `D3d11va`, `Vaapi`, `Qsv`, `Amf`

### Resource Disposal

```csharp
runner.Dispose();

// or using statement
using var runner = new FFmpegBuilder()
    .FromSource("input.mp4")
    .ToFile("output.mp4")
    .Build();
```

## Full Documentation

For complete documentation and sample projects, visit the [GitHub repository](https://github.com/ffmpeg-runner/FFmpegRunner).