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

Uses `IFrameSplitter` to split raw byte streams at frame boundaries, then `IFrameAnalyzer` to identify frame types. The default `CompositeFrameSplitter` uses a chain of responsibility (H.264 → H.265 → MJPEG), which can be customized:

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

#### Custom Frame Splitter

Use `WithFrameSplitter()` to precisely control frame boundary detection and support new codec formats:

```csharp
// Only handle H.264 and MJPEG, exclude H.265
var customSplitter = new CompositeFrameSplitter(
    new H264FrameSplitter(),
    new MjpegFrameSplitter()
);

new FFmpegBuilder()
    .FromSource("input.mp4")
    .WithVideoCodec("h264")
    .WithOverwrite(true)
    .ToPipe(pipe => pipe
        .WithPipeType(PipeType.Frame)
        .WithFrameSplitter(customSplitter)
        .WithFrameAnalyzer(new H264FrameAnalyzer())
        .WithCallback((data, metadata) => { ... }))
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

Supported splitters:

| Splitter | Description |
|----------|-------------|
| `H264FrameSplitter` | H.264 Annex B NAL splitting + AU aggregation |
| `H265FrameSplitter` | H.265 Annex B NAL splitting + AU aggregation |
| `MjpegFrameSplitter` | MJPEG SOI/EOI frame boundary detection |
| `CompositeFrameSplitter` | Combines multiple splitters (default, chain of responsibility) |

#### Extending with Custom Splitters

Implement the `IFrameSplitter` interface to register into the chain:

```csharp
public class MyCodecSplitter : IFrameSplitter
{
    public bool TryExtractFrame(List<byte> buffer, out byte[]? frame) { ... }
    public bool TryFlush(out byte[]? frame) { ... }
    public void Reset() { ... }
}

// Register
var splitter = new CompositeFrameSplitter();
splitter.AddSplitter(new MyCodecSplitter());

// Remove a default splitter
splitter.RemoveSplitter<H265FrameSplitter>();
```

### MJPEG Image Capture

With MJPEG encoding, each frame is a complete JPEG image. The underlying `MjpegFrameSplitter` automatically splits frames by SOI(0xFFD8)/EOI(0xFFD9) markers — no additional decoding required:

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

### Exception Handling

All business exceptions in the library use `FFmpegRunnerException`, which provides an `ErrorCode` for precise identification and `ContextData` for diagnostic context:

```csharp
try
{
    var path = FFmpegConfig.GetFFmpegPath();
}
catch (FFmpegRunnerException ex) when (ex.ErrorCode == "FFMPEG_NOT_FOUND")
{
    Console.WriteLine($"FFmpeg not found: {ex.Message}");
}

try
{
    var streams = runner.GetStreamInfo();
}
catch (FFmpegRunnerException ex)
{
    Console.WriteLine($"Query failed (ErrorCode={ex.ErrorCode}): {ex.Message}");
    // ContextData contains ExitCode, SourcePath, etc.
}
```

Common error codes:

| ErrorCode | Description |
|-----------|-------------|
| `FFMPEG_NOT_FOUND` | FFmpeg executable not found |
| `FFPROBE_NOT_FOUND` | ffprobe executable not found |
| `FFPROBE_FAILED` | ffprobe execution failed |
| `FFMPEG_TIMEOUT` | FFmpeg process timed out |
| `FFMPEG_ALREADY_RUNNING` | FFmpeg process is already running |
| `BUILD_MISSING_SOURCE` | Source path not set during build |
| `BUILD_MISSING_TARGET` | Output target not set during build |
| `BUILD_RTSP_FORMAT_CONFLICT` | RTSP streaming mode format conflict |

## Full Documentation

For complete documentation and sample projects, visit the [GitHub repository](https://github.com/ffmpeg-runner/FFmpegRunner).