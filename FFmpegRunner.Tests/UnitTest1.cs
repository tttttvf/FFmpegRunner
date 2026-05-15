using System.Collections.Generic;
using FFmpegRunner;

namespace FFmpegRunner.Tests
{
    public class FFmpegConfigTests
    {
        [Fact]
        public void SetFFmpegPath_ShouldStorePath()
        {
            var testPath = "/usr/bin/ffmpeg";

            FFmpegConfig.SetFFmpegPath(testPath);

            Assert.Equal(testPath, FFmpegConfig.FFmpegPath);

            FFmpegConfig.SetFFmpegPath(null);
        }

        [Fact]
        public void SetFFmpegPath_Null_ShouldClearPath()
        {
            FFmpegConfig.SetFFmpegPath("/usr/bin/ffmpeg");
            FFmpegConfig.SetFFmpegPath(null);

            Assert.Null(FFmpegConfig.FFmpegPath);
        }

        [Fact]
        public void StaticProperty_ShouldRetainValue()
        {
            var path = @"C:\ffmpeg\ffmpeg.exe";
            FFmpegConfig.FFmpegPath = path;

            Assert.Equal(path, FFmpegConfig.FFmpegPath);

            FFmpegConfig.FFmpegPath = null;
        }
    }

    public class FFmpegBuilderTests
    {
        [Fact]
        public void Build_WithoutSource_ShouldThrow()
        {
            var builder = new FFmpegBuilder()
                .ToFile("output.mp4");

            Assert.Throws<InvalidOperationException>(() => builder.Build());
        }

        [Fact]
        public void Build_WithoutTarget_ShouldThrow()
        {
            var builder = new FFmpegBuilder()
                .FromSource("input.mp4");

            Assert.Throws<InvalidOperationException>(() => builder.Build());
        }

        [Fact]
        public void FluentApi_ShouldChainCorrectly()
        {
            var runner = new FFmpegBuilder()
                .WithFFmpegPath("ffmpeg")
                .FromSource("input.mp4")
                .WithVideoCodec("h264")
                .WithAudioCodec("aac")
                .WithVideoBitrate("2M")
                .WithAudioBitrate("128k")
                .WithResolution("1920x1080")
                .WithFrameRate(30)
                .WithCrf(23)
                .WithFormat("mp4")
                .WithOverwrite(true)
                .ToFile("output.mp4")
                .Build();

            Assert.NotNull(runner);
            Assert.Equal("input.mp4", runner.SourcePath);
            Assert.Equal("output.mp4", runner.TargetPath);
            Assert.Equal("ffmpeg", runner.FFmpegPath);
        }

        [Fact]
        public void ToFile_ShouldSetTargetPath()
        {
            var runner = new FFmpegBuilder()
                .FromSource("test.mp4")
                .ToFile("output.avi")
                .Build();

            Assert.Equal("output.avi", runner.TargetPath);
        }

        [Fact]
        public void ToPipe_ShouldEnablePipeOutput()
        {
            var runner = new FFmpegBuilder()
                .FromSource("test.mp4")
                .ToPipe()
                .Build();

            Assert.Equal("pipe", runner.TargetPath);
            Assert.NotNull(runner.Pipe);
            Assert.IsType<StreamPipe>(runner.Pipe);
        }

        [Fact]
        public void ToPipe_WithCustomName_ShouldBuild()
        {
            var runner = new FFmpegBuilder()
                .FromSource("test.mp4")
                .ToPipe("custom-pipe-name")
                .Build();

            Assert.Equal("pipe", runner.TargetPath);
            Assert.NotNull(runner.Pipe);
            Assert.Equal("custom-pipe-name", runner.Pipe.PipeName);
        }

        [Fact]
        public void ToNetwork_ShouldSetTargetPath()
        {
            var runner = new FFmpegBuilder()
                .FromSource("test.mp4")
                .ToNetwork("rtmp://live.example.com/stream")
                .Build();

            Assert.Equal("rtmp://live.example.com/stream", runner.TargetPath);
        }

        [Fact]
        public void WithCustomArguments_ShouldBeIncluded()
        {
            var runner = new FFmpegBuilder()
                .FromSource("input.mp4")
                .WithCustomArguments("-vf scale=1280:720 -g 30")
                .ToFile("output.mp4")
                .Build();

            Assert.Contains("-vf scale=1280:720 -g 30", runner.CommandArguments);
        }

        [Fact]
        public void WithPipeDataCallback_ShouldWireUpEvent()
        {
            var callbackFired = false;
            byte[]? receivedData = null;
            FrameMetadata? receivedMetadata = null;

            var runner = new FFmpegBuilder()
                .FromSource("input.mp4")
                .WithVideoCodec("h264")
                .WithFormat("flv")
                .ToPipe(pipe => pipe.WithCallback((data, metadata) =>
                {
                    callbackFired = true;
                    receivedData = data;
                    receivedMetadata = metadata;
                }))
                .Build();

            Assert.NotNull(runner);
            Assert.NotNull(runner.Pipe);

            var testData = new byte[] { 0x00, 0x01, 0x02 };

            typeof(StreamPipe)
                .GetMethod("OnDataReceived",
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                ?.Invoke(runner.Pipe, new object[] { testData });

            Assert.True(callbackFired);
            Assert.Equal(testData, receivedData);
            Assert.Null(receivedMetadata);
        }

        [Fact]
        public void NetworkProtocolSettings_ShouldBeIncluded()
        {
            var runner = new FFmpegBuilder()
                .FromSource("rtsp://camera.local/stream")
                .WithNetworkTimeout(5000000)
                .WithBufferSize(65536)
                .WithRtspTransport("tcp")
                .WithVideoCodec("copy")
                .ToFile("output.mp4")
                .Build();

            Assert.Contains("-timeout 5000000", runner.CommandArguments);
            Assert.Contains("-buffer_size 65536", runner.CommandArguments);
            Assert.Contains("-rtsp_transport tcp", runner.CommandArguments);
        }

        [Fact]
        public void AudioSettings_ShouldBeIncluded()
        {
            var runner = new FFmpegBuilder()
                .FromSource("input.wav")
                .WithAudioCodec("mp3")
                .WithAudioBitrate("320k")
                .WithAudioSampleRate(44100)
                .WithAudioChannels(2)
                .ToFile("output.mp3")
                .Build();

            Assert.Contains("-c:a mp3", runner.CommandArguments);
            Assert.Contains("-b:a 320k", runner.CommandArguments);
            Assert.Contains("-ar 44100", runner.CommandArguments);
            Assert.Contains("-ac 2", runner.CommandArguments);
        }

        [Fact]
        public void VideoSettings_ShouldBeIncluded()
        {
            var runner = new FFmpegBuilder()
                .FromSource("input.mp4")
                .WithVideoCodec("h265")
                .WithVideoBitrate("4M")
                .WithResolution("3840x2160")
                .WithFrameRate(60)
                .WithCrf(18)
                .WithPreset("slow")
                .WithPixelFormat("yuv420p10le")
                .WithThreads(4)
                .ToFile("output.mp4")
                .Build();

            Assert.Contains("-c:v h265", runner.CommandArguments);
            Assert.Contains("-b:v 4M", runner.CommandArguments);
            Assert.Contains("-s 3840x2160", runner.CommandArguments);
            Assert.Contains("-r 60", runner.CommandArguments);
            Assert.Contains("-crf 18", runner.CommandArguments);
            Assert.Contains("-preset slow", runner.CommandArguments);
            Assert.Contains("-pix_fmt yuv420p10le", runner.CommandArguments);
            Assert.Contains("-threads 4", runner.CommandArguments);
        }

        [Fact]
        public void Builder_WithoutOverwrite_ShouldNotIncludeOverwriteFlag()
        {
            var runner = new FFmpegBuilder()
                .FromSource("input.mp4")
                .WithOverwrite(false)
                .ToFile("output.mp4")
                .Build();

            Assert.DoesNotContain("-y", runner.CommandArguments);
        }

        [Fact]
        public void WithBufferCapacity_ShouldSetPipeProperty()
        {
            var runner = new FFmpegBuilder()
                .FromSource("input.mp4")
                .ToPipe(pipe => pipe
                    .WithCallback((_, _) => { })
                    .WithBufferCapacity(200))
                .Build();

            Assert.NotNull(runner.Pipe);
            Assert.Equal(200, runner.Pipe.BufferCapacity);
        }

        [Fact]
        public void WithBufferCapacity_Negative_ShouldClampToOne()
        {
            var runner = new FFmpegBuilder()
                .FromSource("input.mp4")
                .ToPipe(pipe => pipe
                    .WithCallback((_, _) => { })
                    .WithBufferCapacity(-5))
                .Build();

            Assert.NotNull(runner.Pipe);
            Assert.Equal(1, runner.Pipe.BufferCapacity);
        }

        [Fact]
        public void FluentPipeline_ShouldChainAllConfigs()
        {
            var runner = new FFmpegBuilder()
                .FromSource("input.mp4")
                .WithVideoCodec("h264")
                .ToPipe(pipe => pipe
                    .WithCallback((_, _) => { })
                    .WithBufferCapacity(50))
                .Build();

            Assert.NotNull(runner.Pipe);
            Assert.Equal(50, runner.Pipe.BufferCapacity);
        }

        [Fact]
        public void NonPipeTarget_ShouldNotCreatePipe()
        {
            var runner = new FFmpegBuilder()
                .FromSource("input.mp4")
                .ToFile("output.mp4")
                .Build();

            Assert.Equal("output.mp4", runner.TargetPath);
            Assert.Null(runner.Pipe);
        }

        [Fact]
        public void ToPipe_WithPipeType_Frame_ShouldCreateFramePipe()
        {
            var runner = new FFmpegBuilder()
                .FromSource("input.mp4")
                .ToPipe(pipe => pipe
                    .WithCallback((_, _) => { })
                    .WithPipeType(PipeType.Frame))
                .Build();

            Assert.NotNull(runner.Pipe);
            Assert.IsType<FramePipe>(runner.Pipe);
        }

        [Fact]
        public void ToPipe_WithPipeType_Stream_ShouldCreateStreamPipe()
        {
            var runner = new FFmpegBuilder()
                .FromSource("input.mp4")
                .ToPipe(pipe => pipe
                    .WithCallback((_, _) => { })
                    .WithPipeType(PipeType.Stream))
                .Build();

            Assert.NotNull(runner.Pipe);
            Assert.IsType<StreamPipe>(runner.Pipe);
        }

        [Fact]
        public void ToPipe_DefaultPipeType_ShouldBeStream()
        {
            var runner = new FFmpegBuilder()
                .FromSource("input.mp4")
                .ToPipe(pipe => pipe.WithCallback((_, _) => { }))
                .Build();

            Assert.NotNull(runner.Pipe);
            Assert.IsType<StreamPipe>(runner.Pipe);
        }
    }

    public class FFmpegRunnerTests
    {
        [Fact]
        public void Constructor_ShouldSetProperties()
        {
            var runner = new FFmpegRunner(
                "ffmpeg",
                "source.mp4",
                "-c:v h264 -b:v 2M",
                "output.mp4");

            Assert.Equal("ffmpeg", runner.FFmpegPath);
            Assert.Equal("source.mp4", runner.SourcePath);
            Assert.Equal("-c:v h264 -b:v 2M", runner.CommandArguments);
            Assert.Equal("output.mp4", runner.TargetPath);
            Assert.Null(runner.Process);
        }

        [Fact]
        public void Constructor_WithPipe_ShouldSetPipe()
        {
            var pipe = new StreamPipe("my-pipe");
            var runner = new FFmpegRunner(
                "ffmpeg",
                "source.mp4",
                "-c:v h264",
                "pipe",
                pipe);

            Assert.Equal("pipe", runner.TargetPath);
            Assert.NotNull(runner.Pipe);
            Assert.Equal("my-pipe", runner.Pipe!.PipeName);
            Assert.Null(runner.Process);
        }

        [Fact]
        public void Constructor_NullFFmpegPath_ShouldUseConfig()
        {
            FFmpegConfig.SetFFmpegPath("configured_ffmpeg.exe");

            try
            {
                var runner = new FFmpegRunner(
                    null,
                    "source.mp4",
                    "-c copy",
                    "output.mp4");

                Assert.Equal("configured_ffmpeg.exe", runner.FFmpegPath);
            }
            finally
            {
                FFmpegConfig.SetFFmpegPath(null);
            }
        }

        [Fact]
        public void Constructor_NullSourcePath_ShouldThrow()
        {
            Assert.Throws<ArgumentNullException>(() =>
                new FFmpegRunner("ffmpeg", null!, "-c copy", "output.mp4"));
        }

        [Fact]
        public void Constructor_NullArguments_ShouldThrow()
        {
            Assert.Throws<ArgumentNullException>(() =>
                new FFmpegRunner("ffmpeg", "source.mp4", null!, "output.mp4"));
        }

        [Fact]
        public void Constructor_NullTargetPath_ShouldThrow()
        {
            Assert.Throws<ArgumentNullException>(() =>
                new FFmpegRunner("ffmpeg", "source.mp4", "-c copy", null!));
        }

        [Fact]
        public void TimeoutMilliseconds_DefaultShouldBeZero()
        {
            var runner = new FFmpegRunner("ffmpeg", "src.mp4", "-c copy", "out.mp4");

            Assert.Equal(0, runner.TimeoutMilliseconds);
        }

        [Fact]
        public void TimeoutMilliseconds_ShouldBeSettable()
        {
            var runner = new FFmpegRunner("ffmpeg", "src.mp4", "-c copy", "out.mp4")
            {
                TimeoutMilliseconds = 30000
            };

            Assert.Equal(30000, runner.TimeoutMilliseconds);
        }

        [Fact]
        public void PipeDataReceived_ThroughPipe_ShouldBeSubscribable()
        {
            var pipe = new StreamPipe("test-pipe");
            var runner = new FFmpegRunner(
                "ffmpeg",
                "src.mp4",
                "-c copy",
                "pipe",
                pipe);

            var eventFired = false;

            runner.Pipe!.DataReceived += (_, e) =>
            {
                eventFired = true;
                Assert.NotNull(e.Data);
            };

            typeof(StreamPipe)
                .GetMethod("OnDataReceived",
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                ?.Invoke(runner.Pipe, new object[] { new byte[] { 0xAA, 0xBB } });

            Assert.True(eventFired);
        }

        [Fact]
        public void Dispose_ShouldCleanupResources()
        {
            var runner = new FFmpegRunner("ffmpeg", "src.mp4", "-c copy", "out.mp4");

            runner.Dispose();

            Assert.Null(runner.Process);
        }
    }

    public class StreamPipeTests
    {
        [Fact]
        public void Constructor_ShouldSetPipeName()
        {
            var pipe = new StreamPipe("test-pipe");

            Assert.Equal("test-pipe", pipe.PipeName);
            Assert.Equal(100, pipe.BufferCapacity);
        }

        [Fact]
        public void BufferCapacity_ShouldBeSettable()
        {
            var pipe = new StreamPipe("test-pipe")
            {
                BufferCapacity = 200
            };

            Assert.Equal(200, pipe.BufferCapacity);
        }

        [Fact]
        public void GetOutputTarget_OnWindows_ShouldReturnWindowsPath()
        {
            var pipe = new StreamPipe("my-pipe");
            var target = pipe.GetOutputTarget();

            if (System.Runtime.InteropServices.RuntimeInformation.IsOSPlatform(System.Runtime.InteropServices.OSPlatform.Windows))
            {
                Assert.Equal(@"\\.\pipe\my-pipe", target);
            }
            else
            {
                Assert.Contains("my-pipe", target);
            }
        }

        [Fact]
        public void DataReceived_ShouldFireEvent()
        {
            var pipe = new StreamPipe("test-pipe");
            var fired = false;
            byte[]? received = null;

            pipe.DataReceived += (_, e) =>
            {
                fired = true;
                received = e.Data;
            };

            typeof(StreamPipe)
                .GetMethod("OnDataReceived",
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                ?.Invoke(pipe, new object[] { new byte[] { 0x01, 0x02 } });

            Assert.True(fired);
            Assert.Equal(new byte[] { 0x01, 0x02 }, received);
        }

        [Fact]
        public void Initialize_ShouldNotThrow()
        {
            using var pipe = new StreamPipe(Guid.NewGuid().ToString("N"));
            pipe.Initialize();
        }

        [Fact]
        public void Stop_WithoutStart_ShouldNotThrow()
        {
            var pipe = new StreamPipe("test-pipe");
            pipe.Stop();
        }

        [Fact]
        public void Dispose_ShouldCleanup()
        {
            var pipe = new StreamPipe(Guid.NewGuid().ToString("N"));
            pipe.Initialize();
            pipe.Dispose();
        }
    }

    public class FramePipeTests
    {
        [Fact]
        public void Constructor_ShouldSetPipeName()
        {
            var pipe = new FramePipe("frame-pipe");

            Assert.Equal("frame-pipe", pipe.PipeName);
            Assert.Equal(100, pipe.BufferCapacity);
        }

        [Fact]
        public void BufferCapacity_ShouldBeSettable()
        {
            var pipe = new FramePipe("frame-pipe")
            {
                BufferCapacity = 50
            };

            Assert.Equal(50, pipe.BufferCapacity);
        }

        [Fact]
        public void GetOutputTarget_ShouldReturnValidPath()
        {
            var pipe = new FramePipe("frame-pipe");
            var target = pipe.GetOutputTarget();

            Assert.NotNull(target);
            Assert.Contains("frame-pipe", target);
        }

        [Fact]
        public void DataReceived_ShouldFireEvent()
        {
            var pipe = new FramePipe("test-pipe");
            var fired = false;
            byte[]? received = null;

            pipe.DataReceived += (_, e) =>
            {
                fired = true;
                received = e.Data;
            };

            typeof(FramePipe)
                .GetMethod("OnDataReceived",
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                ?.Invoke(pipe, new object[] { new byte[] { 0xFF, 0xEE } });

            Assert.True(fired);
            Assert.Equal(new byte[] { 0xFF, 0xEE }, received);
        }

        [Fact]
        public void Initialize_ShouldNotThrow()
        {
            using var pipe = new FramePipe(Guid.NewGuid().ToString("N"));
            pipe.Initialize();
        }

        [Fact]
        public void Stop_WithoutStart_ShouldNotThrow()
        {
            var pipe = new FramePipe("test-pipe");
            pipe.Stop();
        }

        [Fact]
        public void Dispose_ShouldCleanup()
        {
            var pipe = new FramePipe(Guid.NewGuid().ToString("N"));
            pipe.Initialize();
            pipe.Dispose();
        }
    }

    public class PipeTargetTests
    {
        [Fact]
        public void WithPipeType_Stream_ShouldConfigureStream()
        {
            var pipeTarget = new PipeTarget();
            pipeTarget.WithPipeType(PipeType.Stream);

            Assert.Equal(PipeType.Stream, pipeTarget.GetType()
                .GetProperty("PipeType", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                ?.GetValue(pipeTarget));
        }

        [Fact]
        public void WithPipeType_Frame_ShouldConfigureFrame()
        {
            var pipeTarget = new PipeTarget();
            pipeTarget.WithPipeType(PipeType.Frame);

            Assert.Equal(PipeType.Frame, pipeTarget.GetType()
                .GetProperty("PipeType", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                ?.GetValue(pipeTarget));
        }

        [Fact]
        public void DefaultPipeType_ShouldBeStream()
        {
            var pipeTarget = new PipeTarget();

            Assert.Equal(PipeType.Stream, pipeTarget.GetType()
                .GetProperty("PipeType", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                ?.GetValue(pipeTarget));
        }

        [Fact]
        public void FluentChain_WithPipeType()
        {
            var runner = new FFmpegBuilder()
                .FromSource("input.mp4")
                .ToPipe(pipe => pipe
                    .WithPipeType(PipeType.Frame)
                    .WithBufferCapacity(300)
                    .WithPipeName("my-frame-pipe"))
                .Build();

            Assert.NotNull(runner.Pipe);
            Assert.IsType<FramePipe>(runner.Pipe);
            Assert.Equal("my-frame-pipe", runner.Pipe.PipeName);
            Assert.Equal(300, runner.Pipe.BufferCapacity);
        }
    }

    public class StreamInfoTests
    {
        [Fact]
        public void StreamInfo_DefaultValues()
        {
            var info = new StreamInfo();

            Assert.Equal(0, info.Index);
            Assert.Equal(string.Empty, info.CodecType);
            Assert.Equal(string.Empty, info.CodecName);
            Assert.Null(info.Width);
            Assert.Null(info.Height);
            Assert.Null(info.BitRate);
            Assert.NotNull(info.Tags);
        }

        [Fact]
        public void StreamInfo_PropertyAssignment()
        {
            var info = new StreamInfo
            {
                Index = 0,
                CodecType = "video",
                CodecName = "h264",
                Width = 1920,
                Height = 1080,
                BitRate = 2000000,
                Duration = 120.5,
                PixFmt = "yuv420p",
                RFrameRate = "30/1",
            };

            Assert.Equal(0, info.Index);
            Assert.Equal("video", info.CodecType);
            Assert.Equal("h264", info.CodecName);
            Assert.Equal(1920, info.Width);
            Assert.Equal(1080, info.Height);
            Assert.Equal(2000000, info.BitRate);
            Assert.Equal(120.5, info.Duration);
            Assert.Equal("yuv420p", info.PixFmt);
            Assert.Equal("30/1", info.RFrameRate);
        }

        [Fact]
        public void FrameEventArgs_ShouldWrapDataAndMetadata()
        {
            var data = new byte[] { 1, 2, 3, 4, 5 };
            var metadata = new FrameMetadata
            {
                Size = data.Length,
                Type = FrameType.I,
                IsKeyFrame = true,
                Width = 1920,
                Height = 1080,
                Timestamp = 1000
            };
            var args = new FrameEventArgs(data, metadata);

            Assert.Equal(data, args.Data);
            Assert.NotNull(args.Metadata);
            Assert.Equal(data.Length, args.Metadata.Size);
            Assert.Equal(FrameType.I, args.Metadata.Type);
            Assert.True(args.Metadata.IsKeyFrame);
            Assert.Equal(1920, args.Metadata.Width);
            Assert.Equal(1080, args.Metadata.Height);
            Assert.Equal(1000, args.Metadata.Timestamp);
        }

        [Fact]
        public void FrameEventArgs_WithoutMetadata_ShouldBeNull()
        {
            var data = new byte[] { 1, 2, 3, 4, 5 };
            var args = new FrameEventArgs(data, null);

            Assert.Equal(data, args.Data);
            Assert.Null(args.Metadata);
        }

        [Fact]
        public void FrameMetadata_DefaultValues()
        {
            var metadata = new FrameMetadata();

            Assert.Equal(0, metadata.Timestamp);
            Assert.Equal(0, metadata.DecodeTimestamp);
            Assert.Equal(FrameType.Unknown, metadata.Type);
            Assert.Equal(0, metadata.Width);
            Assert.Equal(0, metadata.Height);
            Assert.False(metadata.IsKeyFrame);
            Assert.Equal(0, metadata.Size);
        }

        [Fact]
        public void FramePipe_ShouldReturnDefaultMetadata()
        {
            var pipe = new FramePipe("test-pipe");

            FrameEventArgs? receivedArgs = null;
            pipe.DataReceived += (_, e) => receivedArgs = e;

            typeof(FramePipe)
                .GetMethod("OnDataReceived",
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                ?.Invoke(pipe, new object[] { new byte[] { 0xAA, 0xBB, 0xCC, 0xDD } });

            Assert.NotNull(receivedArgs);
            Assert.NotNull(receivedArgs!.Metadata);
            Assert.Equal(4, receivedArgs.Metadata.Size);
            Assert.Equal(FrameType.Unknown, receivedArgs.Metadata.Type);
        }

        [Fact]
        public void StreamPipe_EventArgs_Metadata_ShouldBeNull()
        {
            var pipe = new StreamPipe("test-pipe");

            FrameEventArgs? receivedArgs = null;
            pipe.DataReceived += (_, e) => receivedArgs = e;

            typeof(StreamPipe)
                .GetMethod("OnDataReceived",
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                ?.Invoke(pipe, new object[] { new byte[] { 0x11, 0x22 } });

            Assert.NotNull(receivedArgs);
            Assert.Equal(new byte[] { 0x11, 0x22 }, receivedArgs!.Data);
            Assert.Null(receivedArgs.Metadata);
        }

        [Fact]
        public void Builder_WithCallback_ShouldPassDataAndMetadata()
        {
            var receivedData = new List<byte[]>();
            var receivedMetadata = new List<FrameMetadata?>();

            var runner = new FFmpegBuilder()
                .FromSource("input.mp4")
                .ToPipe(pipe => pipe
                    .WithPipeType(PipeType.Frame)
                    .WithCallback((data, metadata) =>
                    {
                        receivedData.Add(data);
                        receivedMetadata.Add(metadata);
                    }))
                .Build();

            Assert.NotNull(runner.Pipe);

            var testData = new byte[] { 0x01, 0x02, 0x03 };
            typeof(FramePipe)
                .GetMethod("OnDataReceived",
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                ?.Invoke(runner.Pipe, new object[] { testData });

            Assert.Single(receivedData);
            Assert.Equal(testData, receivedData[0]);
            Assert.Single(receivedMetadata);
            Assert.NotNull(receivedMetadata[0]);
            Assert.Equal(3, receivedMetadata[0]!.Size);
        }

        [Fact]
        public void FromRtspSource_ShouldSetSourceAndTransport()
        {
            var runner = new FFmpegBuilder()
                .FromRtspSource("rtsp://192.168.1.100:554/stream")
                .ToFile("output.mp4")
                .Build();

            Assert.NotNull(runner);
            Assert.Contains("-rtsp_transport tcp", runner.CommandArguments);
            Assert.Contains("rtsp://192.168.1.100:554/stream", runner.SourcePath);
        }

        [Fact]
        public void FromRtspSource_WithUdp_ShouldSetTransport()
        {
            var runner = new FFmpegBuilder()
                .FromRtspSource("rtsp://192.168.1.100:554/stream", "udp")
                .ToFile("output.mp4")
                .Build();

            Assert.NotNull(runner);
            Assert.Contains("-rtsp_transport udp", runner.CommandArguments);
        }

        [Fact]
        public void FromRtspSource_WithInvalidTransport_ShouldThrow()
        {
            Assert.Throws<ArgumentException>(() => new FFmpegBuilder()
                .FromRtspSource("rtsp://192.168.1.100:554/stream", "invalid")
                .ToFile("output.mp4")
                .Build());
        }

        [Fact]
        public void FromRtspSource_WithNullAddress_ShouldThrow()
        {
            Assert.Throws<ArgumentNullException>(() => new FFmpegBuilder()
                .FromRtspSource(null!)
                .ToFile("output.mp4"));
        }

        [Fact]
        public void ToRtsp_ShouldSetTargetAndFormat()
        {
            var runner = new FFmpegBuilder()
                .FromSource("input.mp4")
                .ToRtsp("rtsp://192.168.1.100:554/live/stream")
                .Build();

            Assert.NotNull(runner);
            Assert.Contains("-rtsp_transport tcp", runner.CommandArguments);
            Assert.Contains("-f rtsp", runner.CommandArguments);
            Assert.Contains("rtsp://192.168.1.100:554/live/stream", runner.TargetPath);
        }

        [Fact]
        public void ToRtsp_WithUdp_ShouldSetTransport()
        {
            var runner = new FFmpegBuilder()
                .FromSource("input.mp4")
                .ToRtsp("rtsp://192.168.1.100:554/live/stream", "udp")
                .Build();

            Assert.NotNull(runner);
            Assert.Contains("-rtsp_transport udp", runner.CommandArguments);
            Assert.Contains("-f rtsp", runner.CommandArguments);
        }

        [Fact]
        public void ToRtsp_WithInvalidTransport_ShouldThrow()
        {
            Assert.Throws<ArgumentException>(() => new FFmpegBuilder()
                .FromSource("input.mp4")
                .ToRtsp("rtsp://192.168.1.100:554/live/stream", "http"));
        }

        [Fact]
        public void ToRtsp_WithNullAddress_ShouldThrow()
        {
            Assert.Throws<ArgumentNullException>(() => new FFmpegBuilder()
                .FromSource("input.mp4")
                .ToRtsp(null!));
        }

        [Fact]
        public void ToRtsp_AfterWithFormat_ShouldOverrideFormat()
        {
            var runner = new FFmpegBuilder()
                .FromSource("input.mp4")
                .WithFormat("flv")
                .ToRtsp("rtsp://192.168.1.100:554/live/stream")
                .Build();

            Assert.NotNull(runner);
            Assert.Contains("-f rtsp", runner.CommandArguments);
            Assert.DoesNotContain("-f flv", runner.CommandArguments);
        }

        [Fact]
        public void WithFormat_BeforeToRtsp_ShouldBeIgnored()
        {
            var runner = new FFmpegBuilder()
                .FromSource("input.mp4")
                .WithFormat("mp4")
                .ToRtsp("rtsp://192.168.1.100:554/live/stream")
                .Build();

            Assert.NotNull(runner);
            Assert.Contains("-f rtsp", runner.CommandArguments);
            Assert.DoesNotContain("-f mp4", runner.CommandArguments);
        }
    }
}