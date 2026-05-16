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
            Assert.Contains("-rtsp_transport tcp", runner.InputArguments);
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
            Assert.Contains("-rtsp_transport udp", runner.InputArguments);
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

    public class FFmpegBuilderInputParamsTests
    {
        [Fact]
        public void WithFrameRate_ShouldSetInputArgument()
        {
            var runner = new FFmpegBuilder()
                .FromSource("input.mp4", opt => opt.WithFrameRate(30))
                .ToFile("output.mp4")
                .Build();

            Assert.Contains("-r 30", runner.InputArguments);
            Assert.DoesNotContain("-r 30", runner.CommandArguments);
        }

        [Fact]
        public void WithBufferSize_ShouldSetInputArgument()
        {
            var runner = new FFmpegBuilder()
                .FromSource("rtsp://camera.local/stream", opt => opt.WithBufferSize(65536))
                .ToFile("output.mp4")
                .Build();

            Assert.Contains("-buffer_size 65536", runner.InputArguments);
        }

        [Fact]
        public void WithTimeout_ShouldSetInputArgument()
        {
            var runner = new FFmpegBuilder()
                .FromSource("rtsp://camera.local/stream", opt => opt.WithTimeout(5000000))
                .ToFile("output.mp4")
                .Build();

            Assert.Contains("-timeout 5000000", runner.InputArguments);
        }

        [Fact]
        public void WithFormat_ShouldSetInputArgument()
        {
            var runner = new FFmpegBuilder()
                .FromSource("input.h264", opt => opt.WithFormat("h264"))
                .ToFile("output.mp4")
                .Build();

            Assert.Contains("-f h264", runner.InputArguments);
        }

        [Fact]
        public void WithVideoCodec_ShouldSetInputArgument()
        {
            var runner = new FFmpegBuilder()
                .FromSource("input.mp4", opt => opt.WithVideoCodec("h264_cuvid"))
                .ToFile("output.mp4")
                .Build();

            Assert.Contains("-c:v h264_cuvid", runner.InputArguments);
        }

        [Fact]
        public void WithAudioCodec_ShouldSetInputArgument()
        {
            var runner = new FFmpegBuilder()
                .FromSource("input.mp4", opt => opt.WithAudioCodec("aac"))
                .ToFile("output.mp4")
                .Build();

            Assert.Contains("-c:a aac", runner.InputArguments);
        }

        [Fact]
        public void WithHardwareAcceleration_ShouldSetInputArgument()
        {
            var runner = new FFmpegBuilder()
                .FromSource("input.mp4", opt => opt.WithHardwareAcceleration(HardwareAccelerationType.D3d11va))
                .ToFile("output.mp4")
                .Build();

            Assert.Contains("-hwaccel d3d11va", runner.InputArguments);
        }

        [Fact]
        public void WithNoVideo_ShouldSetInputArgument()
        {
            var runner = new FFmpegBuilder()
                .FromSource("input.mp4", opt => opt.WithNoVideo())
                .ToFile("output.mp3")
                .Build();

            Assert.Contains("-vn", runner.InputArguments);
        }

        [Fact]
        public void WithNoAudio_ShouldSetInputArgument()
        {
            var runner = new FFmpegBuilder()
                .FromSource("input.mp4", opt => opt.WithNoAudio())
                .ToFile("output.mp4")
                .Build();

            Assert.Contains("-an", runner.InputArguments);
        }

        [Fact]
        public void WithSeekPosition_ShouldSetInputArgument()
        {
            var runner = new FFmpegBuilder()
                .FromSource("input.mp4", opt => opt.WithSeekPosition("00:01:30"))
                .ToFile("output.mp4")
                .Build();

            Assert.Contains("-ss 00:01:30", runner.InputArguments);
        }

        [Fact]
        public void WithDuration_ShouldSetInputArgument()
        {
            var runner = new FFmpegBuilder()
                .FromSource("input.mp4", opt => opt.WithDuration("300"))
                .ToFile("output.mp4")
                .Build();

            Assert.Contains("-t 300", runner.InputArguments);
        }

        [Fact]
        public void MultipleInputParams_ShouldAllBeInInputArguments()
        {
            var runner = new FFmpegBuilder()
                .FromSource("rtsp://camera.local/stream", opt => opt
                    .WithFrameRate(25)
                    .WithBufferSize(131072)
                    .WithTimeout(10000000)
                    .WithHardwareAcceleration(HardwareAccelerationType.Dxva2))
                .ToFile("output.mp4")
                .Build();

            Assert.Contains("-r 25", runner.InputArguments);
            Assert.Contains("-buffer_size 131072", runner.InputArguments);
            Assert.Contains("-timeout 10000000", runner.InputArguments);
            Assert.Contains("-hwaccel dxva2", runner.InputArguments);
            Assert.True(runner.InputArguments.Length > 0);
        }

        [Fact]
        public void InputParams_ShouldNotAppearInCommandArguments()
        {
            var runner = new FFmpegBuilder()
                .FromSource("input.mp4", opt => opt
                    .WithFrameRate(60)
                    .WithBufferSize(32768))
                .WithVideoCodec("h264")
                .WithAudioCodec("aac")
                .ToFile("output.mp4")
                .Build();

            Assert.DoesNotContain("-r 60", runner.CommandArguments);
            Assert.DoesNotContain("-buffer_size 32768", runner.CommandArguments);
            Assert.Contains("-c:v h264", runner.CommandArguments);
            Assert.Contains("-c:a aac", runner.CommandArguments);
        }

        [Fact]
        public void FromRtspSource_ShouldPutRtspTransportInInputArguments()
        {
            var runner = new FFmpegBuilder()
                .FromRtspSource("rtsp://192.168.1.100:554/stream", "udp")
                .WithVideoCodec("copy")
                .ToFile("output.mp4")
                .Build();

            Assert.Contains("-rtsp_transport udp", runner.InputArguments);
            Assert.DoesNotContain("-rtsp_transport", runner.CommandArguments);
            Assert.Contains("-c:v copy", runner.CommandArguments);
        }

        [Fact]
        public void FromRtspSource_WithInputParams_ShouldCombineInputArguments()
        {
            var runner = new FFmpegBuilder()
                .FromRtspSource("rtsp://192.168.1.100:554/stream", "tcp", opt => opt
                    .WithBufferSize(65536)
                    .WithTimeout(5000000))
                .ToFile("output.mp4")
                .Build();

            Assert.Contains("-rtsp_transport tcp", runner.InputArguments);
            Assert.Contains("-buffer_size 65536", runner.InputArguments);
            Assert.Contains("-timeout 5000000", runner.InputArguments);
        }

        [Fact]
        public void FromSource_WithoutCallback_ShouldHaveEmptyInputArguments()
        {
            var runner = new FFmpegBuilder()
                .FromSource("input.mp4")
                .ToFile("output.mp4")
                .Build();

            Assert.Empty(runner.InputArguments);
        }

        [Fact]
        public void OverwriteTrue_ShouldSetFlagOnRunner()
        {
            var runner = new FFmpegBuilder()
                .FromSource("input.mp4")
                .WithOverwrite(true)
                .ToFile("output.mp4")
                .Build();

            Assert.True(runner.Overwrite);
        }

        [Fact]
        public void OverwriteFalse_ShouldClearFlagOnRunner()
        {
            var runner = new FFmpegBuilder()
                .FromSource("input.mp4")
                .WithOverwrite(false)
                .ToFile("output.mp4")
                .Build();

            Assert.False(runner.Overwrite);
        }

        [Fact]
        public void OverwriteDefault_ShouldBeFalseOnBuilder()
        {
            var runner = new FFmpegBuilder()
                .FromSource("input.mp4")
                .ToFile("output.mp4")
                .Build();

            Assert.False(runner.Overwrite);
        }
    }

    public class FrameAnalyzerTests
    {
        [Fact]
        public void Analyze_EmptyData_ShouldReturnDefaultMetadata()
        {
            var data = Array.Empty<byte>();
            var metadata = FrameAnalyzer.Analyze(data);

            Assert.NotNull(metadata);
            Assert.Equal(0, metadata.Size);
            Assert.Equal(FrameType.Unknown, metadata.Type);
            Assert.False(metadata.IsKeyFrame);
        }

        [Fact]
        public void Analyze_SmallData_ShouldReturnSizeOnly()
        {
            var data = new byte[] { 0x01, 0x02, 0x03 };
            var metadata = FrameAnalyzer.Analyze(data);

            Assert.NotNull(metadata);
            Assert.Equal(3, metadata.Size);
            Assert.Equal(FrameType.Unknown, metadata.Type);
        }

        [Fact]
        public void Analyze_H264IdrFrame_ShouldDetectIFrame()
        {
            var nalUnit = new byte[]
            {
                0x00, 0x00, 0x00, 0x01,
                0x65
            };
            var metadata = FrameAnalyzer.Analyze(nalUnit);

            Assert.NotNull(metadata);
            Assert.Equal(FrameType.I, metadata.Type);
            Assert.True(metadata.IsKeyFrame);
        }

        [Fact]
        public void Analyze_H264NonIdrFrame_ShouldDetectPFrame()
        {
            var nalUnit = new byte[]
            {
                0x00, 0x00, 0x00, 0x01,
                0x41
            };
            var metadata = FrameAnalyzer.Analyze(nalUnit);

            Assert.NotNull(metadata);
            Assert.Equal(FrameType.P, metadata.Type);
            Assert.False(metadata.IsKeyFrame);
        }

        [Fact]
        public void Analyze_H264WithSps_ShouldDetectIFrame()
        {
            var nalUnits = new byte[]
            {
                0x00, 0x00, 0x00, 0x01,
                0x67,
                0x00, 0x00, 0x00, 0x01,
                0x68,
                0x00, 0x00, 0x00, 0x01,
                0x65
            };
            var metadata = FrameAnalyzer.Analyze(nalUnits);

            Assert.NotNull(metadata);
            Assert.Equal(FrameType.I, metadata.Type);
            Assert.True(metadata.IsKeyFrame);
        }

        [Fact]
        public void Analyze_JpegData_ShouldDetectIFrame()
        {
            var jpeg = new byte[] { 0xFF, 0xD8, 0xFF, 0xE0, 0x00, 0x10, 0x4A, 0x46, 0x49, 0x46 };

            var metadata = FrameAnalyzer.Analyze(jpeg);

            Assert.NotNull(metadata);
            Assert.Equal(FrameType.I, metadata.Type);
            Assert.True(metadata.IsKeyFrame);
        }

        [Fact]
        public void IsAudioFrame_Mp3Frame_ShouldReturnTrue()
        {
            var mp3Frame = new byte[] { 0xFF, 0xFB, 0x90, 0x00 };

            var result = FrameAnalyzer.IsAudioFrame(mp3Frame);

            Assert.True(result);
        }

        [Fact]
        public void IsAudioFrame_AacFrame_ShouldReturnTrue()
        {
            var aacFrame = new byte[] { 0xFF, 0xF1, 0x50, 0x80 };

            var result = FrameAnalyzer.IsAudioFrame(aacFrame);

            Assert.True(result);
        }

        [Fact]
        public void IsAudioFrame_Ac3Frame_ShouldReturnTrue()
        {
            var ac3Frame = new byte[] { 0x0B, 0x77, 0x00, 0x00, 0x00, 0x00 };

            var result = FrameAnalyzer.IsAudioFrame(ac3Frame);

            Assert.True(result);
        }

        [Fact]
        public void IsAudioFrame_NonAudio_ShouldReturnFalse()
        {
            var data = new byte[] { 0x00, 0x01, 0x02, 0x03 };

            var result = FrameAnalyzer.IsAudioFrame(data);

            Assert.False(result);
        }

        [Fact]
        public void IsAudioFrame_SmallData_ShouldReturnFalse()
        {
            var data = new byte[] { 0xFF };

            var result = FrameAnalyzer.IsAudioFrame(data);

            Assert.False(result);
        }

        [Fact]
        public void Analyze_H265IdrFrame_ShouldDetectIFrame()
        {
            var nalUnit = new byte[]
            {
                0x00, 0x00, 0x00, 0x01,
                0x26, 0x01
            };
            var metadata = FrameAnalyzer.Analyze(nalUnit);

            Assert.NotNull(metadata);
            Assert.Equal(FrameType.I, metadata.Type);
            Assert.True(metadata.IsKeyFrame);
        }
    }

    public class FramePipeAdvancedTests
    {
        [Fact]
        public void MaxFrameSize_Default_ShouldBe100MB()
        {
            var pipe = new FramePipe("test-pipe");

            Assert.Equal(100 * 1024 * 1024, pipe.MaxFrameSize);
        }

        [Fact]
        public void MaxFrameSize_Custom_ShouldBeSet()
        {
            var pipe = new FramePipe("test-pipe")
            {
                MaxFrameSize = 1024 * 1024
            };

            Assert.Equal(1024 * 1024, pipe.MaxFrameSize);
        }

        [Fact]
        public void MaxFrameSize_BelowMinimum_ShouldClamp()
        {
            var pipe = new FramePipe("test-pipe")
            {
                MaxFrameSize = 100
            };

            Assert.Equal(1024, pipe.MaxFrameSize);
        }

        [Fact]
        public void ReadTimeoutMilliseconds_Default_ShouldBe5000()
        {
            var pipe = new FramePipe("test-pipe");

            Assert.Equal(5000, pipe.ReadTimeoutMilliseconds);
        }

        [Fact]
        public void ReadTimeoutMilliseconds_Custom_ShouldBeSet()
        {
            var pipe = new FramePipe("test-pipe")
            {
                ReadTimeoutMilliseconds = 10000
            };

            Assert.Equal(10000, pipe.ReadTimeoutMilliseconds);
        }

        [Fact]
        public void ReadTimeoutMilliseconds_Zero_ShouldBeSet()
        {
            var pipe = new FramePipe("test-pipe")
            {
                ReadTimeoutMilliseconds = 0
            };

            Assert.Equal(0, pipe.ReadTimeoutMilliseconds);
        }

        [Fact]
        public void DataReceived_WithH264Idr_ShouldHaveCorrectMetadata()
        {
            var pipe = new FramePipe("test-pipe");

            FrameEventArgs? receivedArgs = null;
            pipe.DataReceived += (_, e) => receivedArgs = e;

            var h264Idr = new byte[]
            {
                0x00, 0x00, 0x00, 0x01,
                0x65, 0x88, 0x84, 0x00,
                0x00, 0x00, 0x00, 0x00
            };

            typeof(FramePipe)
                .GetMethod("OnDataReceived",
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                ?.Invoke(pipe, new object[] { h264Idr });

            Assert.NotNull(receivedArgs);
            Assert.NotNull(receivedArgs!.Metadata);
            Assert.Equal(FrameType.I, receivedArgs.Metadata.Type);
            Assert.True(receivedArgs.Metadata.IsKeyFrame);
        }

        [Fact]
        public void DataReceived_WithAudioFrame_ShouldHaveAudioMetadata()
        {
            var pipe = new FramePipe("test-pipe");

            FrameEventArgs? receivedArgs = null;
            pipe.DataReceived += (_, e) => receivedArgs = e;

            var mp3Frame = new byte[]
            {
                0xFF, 0xFB, 0x90, 0x00,
                0x00, 0x00, 0x00, 0x00
            };

            typeof(FramePipe)
                .GetMethod("OnDataReceived",
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                ?.Invoke(pipe, new object[] { mp3Frame });

            Assert.NotNull(receivedArgs);
            Assert.NotNull(receivedArgs!.Metadata);
            Assert.Equal(FrameType.Audio, receivedArgs.Metadata.Type);
        }
    }

    public class FFmpegBuilderDefaultCodecTests
    {
        [Fact]
        public void Build_WithoutVideoCodec_ShouldAddDefaultH264()
        {
            var runner = new FFmpegBuilder()
                .FromSource("input.mp4")
                .WithAudioCodec("aac")
                .ToFile("output.mp4")
                .Build();

            Assert.Contains("-c:v libx264", runner.CommandArguments);
            Assert.Contains("-c:a aac", runner.CommandArguments);
        }

        [Fact]
        public void Build_WithVideoCodec_ShouldNotAddDefault()
        {
            var runner = new FFmpegBuilder()
                .FromSource("input.mp4")
                .WithVideoCodec("h264_nvenc")
                .ToFile("output.mp4")
                .Build();

            Assert.Contains("-c:v h264_nvenc", runner.CommandArguments);
            Assert.DoesNotContain("libx264", runner.CommandArguments);
        }

        [Fact]
        public void Build_WithVideoCodecCopy_ShouldNotAddDefault()
        {
            var runner = new FFmpegBuilder()
                .FromSource("input.mp4")
                .WithVideoCodec("copy")
                .ToFile("output.mp4")
                .Build();

            Assert.Contains("-c:v copy", runner.CommandArguments);
            Assert.DoesNotContain("libx264", runner.CommandArguments);
        }

        [Fact]
        public void Build_WithCustomArgsVideoCodec_ShouldNotAddDefault()
        {
            var runner = new FFmpegBuilder()
                .FromSource("input.mp4")
                .WithCustomArguments("-c:v hevc")
                .ToFile("output.mp4")
                .Build();

            Assert.Contains("-c:v hevc", runner.CommandArguments);
            Assert.DoesNotContain("libx264", runner.CommandArguments);
        }

        [Fact]
        public void WithoutVideoCodec_ShouldRemoveCodecFromArguments()
        {
            var runner = new FFmpegBuilder()
                .FromSource("input.mp4")
                .WithVideoCodec("h264_nvenc")
                .WithoutVideoCodec()
                .ToFile("output.mp4")
                .Build();

            Assert.DoesNotContain("nvenc", runner.CommandArguments);
            Assert.Contains("-c:v libx264", runner.CommandArguments);
        }

        [Fact]
        public void WithoutVideoCodec_ShouldAllowReaddingDefault()
        {
            var runner = new FFmpegBuilder()
                .FromSource("input.mp4")
                .WithVideoCodec("hevc")
                .WithoutVideoCodec()
                .ToFile("output.mp4")
                .Build();

            Assert.DoesNotContain("hevc", runner.CommandArguments);
        }

        [Fact]
        public void Build_WithPipeOutput_ShouldAlsoAddDefaultCodec()
        {
            var runner = new FFmpegBuilder()
                .FromSource("input.mp4")
                .ToPipe("test-pipe-default-codec")
                .Build();

            Assert.Contains("-c:v libx264", runner.CommandArguments);
        }
    }

    public class FrameAnalyzerInterfaceTests
    {
        [Fact]
        public void H264Analyzer_ShouldDetectIdrFrame()
        {
            var analyzer = new H264FrameAnalyzer();

            var data = new byte[]
            {
                0x00, 0x00, 0x00, 0x01, 0x65,
                0x00, 0x00, 0x00, 0x00, 0x00
            };

            var result = analyzer.TryAnalyze(data, out var metadata);

            Assert.True(result);
            Assert.NotNull(metadata);
            Assert.Equal(FrameType.I, metadata.Type);
            Assert.True(metadata.IsKeyFrame);
        }

        [Fact]
        public void H264Analyzer_ShouldDetectNonIdrFrame()
        {
            var analyzer = new H264FrameAnalyzer();

            var data = new byte[]
            {
                0x00, 0x00, 0x00, 0x01, 0x41,
                0x00, 0x00, 0x00, 0x00, 0x00
            };

            var result = analyzer.TryAnalyze(data, out var metadata);

            Assert.True(result);
            Assert.NotNull(metadata);
            Assert.False(metadata.IsKeyFrame);
        }

        [Fact]
        public void H264Analyzer_SmallData_ShouldReturnFalse()
        {
            var analyzer = new H264FrameAnalyzer();
            var result = analyzer.TryAnalyze(new byte[] { 0x00 }, out _);
            Assert.False(result);
        }

        [Fact]
        public void H265Analyzer_ShouldDetectIdrFrame()
        {
            var analyzer = new H265FrameAnalyzer();

            var data = new byte[]
            {
                0x00, 0x00, 0x00, 0x01, 0x26, 0x01,
                0x00, 0x00, 0x00, 0x00, 0x00, 0x00
            };

            var result = analyzer.TryAnalyze(data, out var metadata);

            Assert.True(result);
            Assert.NotNull(metadata);
            Assert.Equal(FrameType.I, metadata.Type);
            Assert.True(metadata.IsKeyFrame);
        }

        [Fact]
        public void H265Analyzer_ShouldDetectNonIdrFrame()
        {
            var analyzer = new H265FrameAnalyzer();

            var data = new byte[]
            {
                0x00, 0x00, 0x00, 0x01, 0x02, 0x01,
                0x00, 0x00, 0x00, 0x00, 0x00, 0x00
            };

            var result = analyzer.TryAnalyze(data, out var metadata);

            Assert.True(result);
            Assert.NotNull(metadata);
            Assert.False(metadata.IsKeyFrame);
        }

        [Fact]
        public void H265Analyzer_SmallData_ShouldReturnFalse()
        {
            var analyzer = new H265FrameAnalyzer();
            var result = analyzer.TryAnalyze(new byte[] { 0x00, 0x00 }, out _);
            Assert.False(result);
        }

        [Fact]
        public void H265Analyzer_ShouldDetectBlaFrame()
        {
            var analyzer = new H265FrameAnalyzer();

            var data = new byte[]
            {
                0x00, 0x00, 0x00, 0x01, 0x20, 0x01,
                0x00, 0x00, 0x00, 0x00, 0x00, 0x00
            };

            var result = analyzer.TryAnalyze(data, out var metadata);

            Assert.True(result);
            Assert.NotNull(metadata);
            Assert.Equal(FrameType.I, metadata.Type);
            Assert.True(metadata.IsKeyFrame);
        }

        [Fact]
        public void H265Analyzer_ShouldDetectCraFrame()
        {
            var analyzer = new H265FrameAnalyzer();

            var data = new byte[]
            {
                0x00, 0x00, 0x00, 0x01, 0x2A, 0x01,
                0x00, 0x00, 0x00, 0x00, 0x00, 0x00
            };

            var result = analyzer.TryAnalyze(data, out var metadata);

            Assert.True(result);
            Assert.NotNull(metadata);
            Assert.Equal(FrameType.I, metadata.Type);
            Assert.True(metadata.IsKeyFrame);
        }

        [Fact]
        public void MjpegAnalyzer_ShouldDetectJpegFrame()
        {
            var analyzer = new MjpegFrameAnalyzer();

            var data = new byte[]
            {
                0xFF, 0xD8, 0xFF, 0xE0,
                0x00, 0x10, 0x4A, 0x46,
                0xFF, 0xD9
            };

            var result = analyzer.TryAnalyze(data, out var metadata);

            Assert.True(result);
            Assert.NotNull(metadata);
            Assert.Equal(FrameType.I, metadata.Type);
            Assert.True(metadata.IsKeyFrame);
        }

        [Fact]
        public void MjpegAnalyzer_NonJpegData_ShouldReturnFalse()
        {
            var analyzer = new MjpegFrameAnalyzer();
            var result = analyzer.TryAnalyze(new byte[] { 0x00, 0x01, 0x02, 0x03 }, out _);
            Assert.False(result);
        }

        [Fact]
        public void CompositeAnalyzer_Default_ContainsAnalyzers()
        {
            var composite = new CompositeFrameAnalyzer();

            Assert.Equal(3, composite.Count);

            var idrData = new byte[]
            {
                0x00, 0x00, 0x00, 0x01, 0x65,
                0x00, 0x00, 0x00, 0x00, 0x00
            };

            var result = composite.TryAnalyze(idrData, out var metadata);
            Assert.True(result);
            Assert.True(metadata!.IsKeyFrame);
        }

        [Fact]
        public void CompositeAnalyzer_CustomAnalyzers_ShouldBeUsed()
        {
            var composite = new CompositeFrameAnalyzer(new MjpegFrameAnalyzer());

            var jpegData = new byte[] { 0xFF, 0xD8, 0xFF, 0xE0, 0x00, 0x10, 0x4A, 0x46, 0xFF, 0xD9 };
            var result = composite.TryAnalyze(jpegData, out var metadata);
            Assert.True(result);
        }

        [Fact]
        public void CompositeAnalyzer_AddAnalyzer_ShouldIncreaseCount()
        {
            var composite = new CompositeFrameAnalyzer();

            composite.AddAnalyzer(new H264FrameAnalyzer());

            Assert.Equal(4, composite.Count);
        }

        [Fact]
        public void CompositeAnalyzer_RemoveAnalyzer_ShouldDecreaseCount()
        {
            var composite = new CompositeFrameAnalyzer();

            var removed = composite.RemoveAnalyzer<H264FrameAnalyzer>();

            Assert.True(removed);
            Assert.Equal(2, composite.Count);
        }

        [Fact]
        public void CompositeAnalyzer_RemoveNonexistent_ShouldReturnFalse()
        {
            var composite = new CompositeFrameAnalyzer(new MjpegFrameAnalyzer());

            var removed = composite.RemoveAnalyzer<H264FrameAnalyzer>();

            Assert.False(removed);
            Assert.Equal(1, composite.Count);
        }

        [Fact]
        public void PipeTarget_WithFrameAnalyzer_ShouldConfigure()
        {
            var target = new PipeTarget();
            var analyzer = new H264FrameAnalyzer();

            typeof(PipeTarget)
                .GetMethod("WithFrameAnalyzer")!
                .Invoke(target, new object?[] { analyzer });

            var storedAnalyzer = typeof(PipeTarget)
                .GetProperty("FrameAnalyzer",
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!
                .GetValue(target);

            Assert.Same(analyzer, storedAnalyzer);
        }

        [Fact]
        public void PipeTarget_WithNullFrameAnalyzer_ShouldDisable()
        {
            var target = new PipeTarget();

            typeof(PipeTarget)
                .GetMethod("WithFrameAnalyzer")!
                .Invoke(target, new object?[] { null });

            var storedAnalyzer = typeof(PipeTarget)
                .GetProperty("FrameAnalyzer",
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!
                .GetValue(target);

            Assert.Null(storedAnalyzer);
        }
    }
}