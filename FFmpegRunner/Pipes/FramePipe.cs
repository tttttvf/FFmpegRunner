using System;
using System.Buffers;
using System.Collections.Generic;
using System.IO;
using System.IO.Pipes;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;

namespace FFmpegRunner
{
    /// <summary>
    /// 帧管道实现类。利用 <see cref="IFrameSplitter"/> 从命名管道读取的原始字节流中
    /// 按帧边界切分数据，并通过 <see cref="IFrameAnalyzer"/> 分析帧类型和元数据。
    /// </summary>
    /// <remarks>
    /// 数据流：FFmpeg → NamedPipeServerStream → IFrameSplitter → Channel → DataReceived 事件 → 用户回调
    /// </remarks>
    public class FramePipe : IPipeInterface
    {
        private int _maxFrameSize = 100 * 1024 * 1024;
        private const int MinMaxFrameSize = 1024;
        private const int BufferSize = 65536;

        private NamedPipeServerStream? _pipeServer;
        private CancellationTokenSource? _cancelCts;
        private Task? _readTask;
        private Task? _consumerTask;
        private Channel<byte[]?>? _channel;
        private bool _disposed;
        private int _readTimeoutMs = 5000;
        private readonly List<byte> _readBuffer = new List<byte>();

        /// <summary>
        /// 获取或设置帧分析器实例，用于解析帧类型和元数据。
        /// 默认使用 <see cref="CompositeFrameAnalyzer"/>（H.264、H.265、MJPEG）。
        /// </summary>
        public IFrameAnalyzer? FrameAnalyzer { get; set; } = new CompositeFrameAnalyzer();

        /// <summary>
        /// 获取或设置帧分割器实例，用于从字节流中按帧边界切分数据。
        /// 默认使用 <see cref="CompositeFrameSplitter"/>（H.264、H.265、MJPEG）。
        /// </summary>
        public IFrameSplitter FrameSplitter { get; set; } = new CompositeFrameSplitter();

        /// <inheritdoc />
        public string PipeName { get; }

        /// <summary>
        /// 获取或设置管道读取超时时间（毫秒）。0 表示无超时。默认值 5000。
        /// </summary>
        public int ReadTimeoutMilliseconds
        {
            get => _readTimeoutMs;
            set => _readTimeoutMs = Math.Max(0, value);
        }

        /// <inheritdoc />
        public int BufferCapacity { get; set; } = 100;

        /// <summary>
        /// 获取或设置最大帧大小（字节）。超过此大小的帧数据将被丢弃。默认值 100 MB。
        /// </summary>
        public int MaxFrameSize
        {
            get => _maxFrameSize;
            set => _maxFrameSize = Math.Max(value, MinMaxFrameSize);
        }

        /// <inheritdoc />
        public event EventHandler<FrameEventArgs>? DataReceived;

        /// <summary>
        /// 初始化 <see cref="FramePipe"/> 类的新实例。
        /// </summary>
        /// <param name="pipeName">管道名称。</param>
        /// <exception cref="ArgumentNullException">当 <paramref name="pipeName"/> 为 <c>null</c> 时抛出。</exception>
        public FramePipe(string pipeName)
        {
            PipeName = pipeName ?? throw new ArgumentNullException(nameof(pipeName));
        }

        /// <inheritdoc />
        public string GetOutputTarget()
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                return $"\\\\.\\pipe\\{PipeName}";
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                return $"pipe:{PipeName}";
            }

            return $"pipe:1";
        }

        /// <inheritdoc />
        public void Initialize()
        {
            _pipeServer = new NamedPipeServerStream(
                PipeName,
                PipeDirection.InOut,
                1,
                PipeTransmissionMode.Byte,
                PipeOptions.Asynchronous,
                BufferSize,
                BufferSize);

            _channel = Channel.CreateBounded<byte[]?>(new BoundedChannelOptions(BufferCapacity)
            {
                FullMode = BoundedChannelFullMode.Wait
            });
        }

        /// <inheritdoc />
        public void Start(CancellationToken cancellationToken)
        {
            if (_pipeServer == null || _channel == null)
                return;

            _cancelCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

            var token = _cancelCts.Token;
            var pipeServer = _pipeServer;
            var writer = _channel.Writer;
            var splitter = FrameSplitter;

            _readTask = Task.Run(async () =>
            {
                var sharedBuffer = ArrayPool<byte>.Shared.Rent(BufferSize);

                try
                {
                    await pipeServer.WaitForConnectionAsync(token).ConfigureAwait(false);

                    while (!token.IsCancellationRequested)
                    {
                        if (!pipeServer_IsConnected(pipeServer))
                            break;

                        var bytesRead = await ReadWithTimeoutAsync(
                            pipeServer, sharedBuffer, token).ConfigureAwait(false);

                        if (bytesRead <= 0)
                            break;

                        for (var i = 0; i < bytesRead; i++)
                            _readBuffer.Add(sharedBuffer[i]);

                        if (_readBuffer.Count > _maxFrameSize)
                        {
                            _readBuffer.Clear();
                            continue;
                        }

                        while (splitter.TryExtractFrame(_readBuffer, out var frame) && frame != null)
                        {
                            writer.TryWrite(frame);
                        }
                    }

                    while (splitter.TryFlush(out var flushed) && flushed != null)
                    {
                        writer.TryWrite(flushed);
                    }
                }
                catch (OperationCanceledException)
                {
                    while (splitter.TryFlush(out var flushed) && flushed != null)
                    {
                        writer.TryWrite(flushed);
                    }
                }
                catch (IOException)
                {
                    while (splitter.TryFlush(out var flushed) && flushed != null)
                    {
                        writer.TryWrite(flushed);
                    }
                }
                catch (ObjectDisposedException)
                {
                }
                finally
                {
                    ArrayPool<byte>.Shared.Return(sharedBuffer);
                    writer.TryComplete();
                }
            }, token);

            var reader = _channel.Reader;

            _consumerTask = Task.Run(async () =>
            {
                try
                {
                    while (await reader.WaitToReadAsync(token).ConfigureAwait(false))
                    {
                        while (reader.TryRead(out var data))
                        {
                            if (data != null)
                                OnDataReceived(data);
                        }
                    }
                }
                catch (OperationCanceledException)
                {
                }
            }, token);
        }

        /// <inheritdoc />
        public void Stop()
        {
            if (_cancelCts != null)
            {
                try
                {
                    _cancelCts.Cancel();
                }
                catch (AggregateException)
                {
                }
                catch (ObjectDisposedException)
                {
                }

                try
                {
                    _cancelCts.Dispose();
                }
                catch (ObjectDisposedException)
                {
                }
            }

            _readBuffer.Clear();
            FrameSplitter.Reset();
            _readTask = null;
            _consumerTask = null;
            _cancelCts = null;
            _channel = null;
        }

        /// <inheritdoc />
        public void Dispose()
        {
            if (_disposed)
                return;

            Stop();

            if (_pipeServer != null)
            {
                try
                {
                    if (_pipeServer.IsConnected)
                    {
                        _pipeServer.Disconnect();
                    }
                }
                catch (IOException)
                {
                }
                catch (ObjectDisposedException)
                {
                }
                catch (InvalidOperationException)
                {
                }

                try
                {
                    _pipeServer.Dispose();
                }
                catch (IOException)
                {
                }
                catch (ObjectDisposedException)
                {
                }

                _pipeServer = null;
            }

            _disposed = true;
        }

        /// <summary>
        /// 触发 DataReceived 事件，使用帧分析器填充元数据。
        /// </summary>
        protected virtual void OnDataReceived(byte[] data)
        {
            FrameMetadata metadata;

            var analyzer = FrameAnalyzer;

            if (analyzer != null && analyzer.IsAudioFrame(data))
            {
                metadata = new FrameMetadata
                {
                    Size = data.Length,
                    Type = FrameType.Audio
                };
            }
            else if (analyzer != null && analyzer.TryAnalyze(data, out var analyzed) && analyzed != null)
            {
                metadata = analyzed;
            }
            else
            {
                metadata = new FrameMetadata
                {
                    Size = data.Length
                };
            }

            DataReceived?.Invoke(this, new FrameEventArgs(data, metadata));
        }

        private async Task<int> ReadWithTimeoutAsync(
            NamedPipeServerStream pipeStream,
            byte[] buffer,
            CancellationToken token)
        {
            if (_readTimeoutMs <= 0)
            {
                return await pipeStream.ReadAsync(buffer, 0, buffer.Length, token).ConfigureAwait(false);
            }

            using var timeoutCts = new CancellationTokenSource(_readTimeoutMs);
            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(token, timeoutCts.Token);

            try
            {
                return await pipeStream.ReadAsync(buffer, 0, buffer.Length, linkedCts.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (!token.IsCancellationRequested)
            {
                return 0;
            }
        }

        private static bool pipeServer_IsConnected(PipeStream pipeStream)
        {
            try
            {
                return pipeStream.IsConnected;
            }
            catch (ObjectDisposedException)
            {
                return false;
            }
        }
    }
}