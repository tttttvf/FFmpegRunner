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
        }

        [Fact]
        public void ToPipe_WithCustomName_ShouldBuild()
        {
            var runner = new FFmpegBuilder()
                .FromSource("test.mp4")
                .ToPipe("custom-pipe-name")
                .Build();

            Assert.Equal("pipe", runner.TargetPath);
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

            FFmpegConfig.SetFFmpegPath(@"E:\ffmpeg-2026-05-13-git-a327bc0561-full_build\bin\ffmpeg.exe");
            var runner = new FFmpegBuilder()
                .FromSource(@"rtmp://liteavapp.qcloud.com/live/liteavdemoplayerstreamid")
                .WithVideoCodec("h264")
                .WithFormat("flv")
                .ToPipe(pipe => pipe.WithCallback(data =>
                {
                    callbackFired = true;
                    receivedData = data;
                }))
                .Build();

            Assert.NotNull(runner);

            var testData = new byte[] { 0x00, 0x01, 0x02 };
            typeof(FFmpegRunner)
                .GetMethod("OnPipeDataReceived",
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                ?.Invoke(runner, new object[] { testData });

            Assert.True(callbackFired);
            Assert.Equal(testData, receivedData);
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
        public void WithBufferCapacity_ShouldSetRunnerProperty()
        {
            var runner = new FFmpegBuilder()
                .FromSource("input.mp4")
                .ToPipe(pipe => pipe
                    .WithCallback(_ => { })
                    .WithBufferCapacity(200))
                .Build();

            Assert.Equal(200, runner.PipeBufferCapacity);
        }

        [Fact]
        public void WithBufferCapacity_Negative_ShouldClampToOne()
        {
            var runner = new FFmpegBuilder()
                .FromSource("input.mp4")
                .ToPipe(pipe => pipe
                    .WithCallback(_ => { })
                    .WithBufferCapacity(-5))
                .Build();

            Assert.Equal(1, runner.PipeBufferCapacity);
        }

        [Fact]
        public void FluentPipeline_ShouldChainAllConfigs()
        {
            var runner = new FFmpegBuilder()
                .FromSource("input.mp4")
                .WithVideoCodec("h264")
                .ToPipe(pipe => pipe
                    .WithCallback(_ => { })
                    .WithBufferCapacity(50))
                .Build();

            Assert.Equal(50, runner.PipeBufferCapacity);
        }

        [Fact]
        public void NonPipeTarget_ShouldNotTriggerPipeEvent()
        {
            var runner = new FFmpegBuilder()
                .FromSource("input.mp4")
                .ToFile("output.mp4")
                .Build();

            Assert.Equal("output.mp4", runner.TargetPath);
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
        public void Constructor_WithPipeOutput_ShouldSetPipeName()
        {
            var runner = new FFmpegRunner(
                "ffmpeg",
                "source.mp4",
                "-c:v h264",
                "pipe",
                usePipeOutput: true,
                pipeName: "my-pipe");

            Assert.Equal("pipe", runner.TargetPath);
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
        public void PipeBufferCapacity_DefaultShouldBe100()
        {
            var runner = new FFmpegRunner("ffmpeg", "src.mp4", "-c copy", "pipe", usePipeOutput: true);

            Assert.Equal(100, runner.PipeBufferCapacity);
        }

        [Fact]
        public void PipeDataReceived_ShouldBeSubscribable()
        {
            var runner = new FFmpegRunner(
                "ffmpeg",
                "src.mp4",
                "-c copy",
                "pipe",
                usePipeOutput: true);

            var eventFired = false;

            runner.PipeDataReceived += (_, e) =>
            {
                eventFired = true;
                Assert.NotNull(e.Data);
            };

            typeof(FFmpegRunner)
                .GetMethod("OnPipeDataReceived",
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                ?.Invoke(runner, new object[] { new byte[] { 0xAA, 0xBB } });

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
        public void PipeDataEventArgs_ShouldWrapData()
        {
            var data = new byte[] { 1, 2, 3, 4, 5 };
            var args = new PipeDataEventArgs(data);

            Assert.Equal(data, args.Data);
        }
    }
}