using System;
using System.Buffers;
using System.IO;
using System.IO.Pipes;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;

namespace FFmpegRunner
{
    /// <summary>
    /// 帧管道实现类。采用长度前缀帧协议进行数据传输。
    /// 每次传输先读取4字节的长度字段（BigEndian），再根据长度读取完整的帧数据。
    /// 适用于需要按帧解析数据的高级场景（如视频帧提取、帧级别的实时处理等）。
    /// </summary>
    /// <remarks>
    /// 帧协议格式：
    /// (4字节长度 BigEndian) (帧数据...)
    /// 数据流：FFmpeg → NamedPipeServerStream → 解析帧协议 → Channel (byte array) → DataReceived 事件 → 用户回调
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
        private Channel<byte[]>? _channel;
        private bool _disposed;
        private int _readTimeoutMs = 5000;

        /// <summary>
        /// 获取或设置帧数据分析器实例。默认使用 <see cref="CompositeFrameAnalyzer"/>
        /// （包含 H.264、H.265 和 MJPEG 分析器）。设置 <c>null</c> 可禁用帧分析，
        /// 仅使用基本的尺寸元数据。
        /// </summary>
        public IFrameAnalyzer? FrameAnalyzer { get; set; } = new CompositeFrameAnalyzer();

        /// <summary>
        /// 获取当前管道的名称。
        /// </summary>
        public string PipeName { get; }

        /// <summary>
        /// 获取或设置单次读取操作的超时时间（毫秒）。默认值 5000ms。
        /// 设置为 0 表示无超时。
        /// </summary>
        public int ReadTimeoutMilliseconds
        {
            get => _readTimeoutMs;
            set => _readTimeoutMs = Math.Max(0, value);
        }

        /// <summary>
        /// 获取或设置管道数据缓冲区的容量（数据块数）。必须在 <see cref="Initialize"/> 之前设置。默认值 100。
        /// </summary>
        public int BufferCapacity { get; set; } = 100;

        /// <summary>
        /// 获取或设置允许的最大帧数据大小（字节）。默认值 100 MB。最小值为 1024 字节。
        /// 超过此大小的帧将被丢弃。
        /// </summary>
        public int MaxFrameSize
        {
            get => _maxFrameSize;
            set => _maxFrameSize = Math.Max(value, MinMaxFrameSize);
        }

        /// <summary>
        /// 当管道读取到完整的帧数据时触发。
        /// </summary>
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

            _channel = Channel.CreateBounded<byte[]>(new BoundedChannelOptions(BufferCapacity)
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

            _readTask = Task.Run(async () =>
            {
                var sharedBuffer = ArrayPool<byte>.Shared.Rent(BufferSize);

                try
                {
                    await pipeServer.WaitForConnectionAsync(token).ConfigureAwait(false);

                    var readTimeout = _readTimeoutMs;

                    while (!token.IsCancellationRequested)
                    {
                        if (!pipeServer_IsConnected(pipeServer))
                            break;

                        CancellationToken readToken = token;
                        CancellationTokenSource? timeoutCts = null;

                        if (readTimeout > 0)
                        {
                            timeoutCts = new CancellationTokenSource(readTimeout);
                            var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(token, timeoutCts.Token);
                            readToken = linkedCts.Token;
                        }

                        try
                        {
                            var frameLength = await ReadFrameLengthAsync(
                                pipeServer, sharedBuffer, readToken).ConfigureAwait(false);

                            if (frameLength <= 0)
                                break;

                            if (frameLength > _maxFrameSize)
                                break;

                            var frameData = await ReadFrameDataAsync(
                                pipeServer, sharedBuffer, frameLength, readToken).ConfigureAwait(false);

                            if (frameData == null)
                                break;

                            await writer.WriteAsync(frameData, token).ConfigureAwait(false);
                        }
                        catch (OperationCanceledException) when (!token.IsCancellationRequested)
                        {
                            continue;
                        }
                        finally
                        {
                            timeoutCts?.Dispose();
                        }
                    }
                }
                catch (OperationCanceledException)
                {
                }
                catch (IOException)
                {
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
        /// 触发 <see cref="DataReceived"/> 事件，使用注入的 <see cref="IFrameAnalyzer"/> 分析帧数据。
        /// </summary>
        /// <param name="data">从管道读取的帧数据。</param>
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

        /// <summary>
        /// 异步读取帧长度字段（4字节 BigEndian）。
        /// </summary>
        /// <param name="pipeStream">管道流。</param>
        /// <param name="buffer">共享缓冲区。</param>
        /// <param name="token">取消令牌。</param>
        /// <returns>帧数据长度，返回 -1 表示连接已断开。</returns>
        private static async Task<int> ReadFrameLengthAsync(
            PipeStream pipeStream,
            byte[] buffer,
            CancellationToken token)
        {
            var offset = 0;
            var zeroReadCount = 0;
            const int maxZeroReads = 10;

            while (offset < 4)
            {
                if (!pipeServer_IsConnected(pipeStream))
                    return -1;

                var bytesRead = await pipeStream.ReadAsync(
                    buffer, offset, 4 - offset, token).ConfigureAwait(false);

                if (bytesRead == 0)
                {
                    zeroReadCount++;

                    if (!pipeServer_IsConnected(pipeStream))
                        return -1;

                    if (zeroReadCount >= maxZeroReads)
                        return -1;

                    continue;
                }

                zeroReadCount = 0;
                offset += bytesRead;
            }

            return ReadInt32BigEndian(buffer, 0);
        }

        /// <summary>
        /// 异步读取指定长度的帧数据。
        /// </summary>
        /// <param name="pipeStream">管道流。</param>
        /// <param name="buffer">共享缓冲区。</param>
        /// <param name="frameLength">期望读取的帧数据长度。</param>
        /// <param name="token">取消令牌。</param>
        /// <returns>读取到的帧数据，返回 <c>null</c> 表示连接已断开。</returns>
        private static async Task<byte[]?> ReadFrameDataAsync(
            PipeStream pipeStream,
            byte[] buffer,
            int frameLength,
            CancellationToken token)
        {
            if (frameLength <= 0)
                return null;

            var frameData = new byte[frameLength];
            var offset = 0;
            var zeroReadCount = 0;
            const int maxZeroReads = 10;

            while (offset < frameLength)
            {
                if (!pipeServer_IsConnected(pipeStream))
                    return null;

                var bytesToRead = Math.Min(buffer.Length, frameLength - offset);
                var bytesRead = await pipeStream.ReadAsync(
                    buffer, 0, bytesToRead, token).ConfigureAwait(false);

                if (bytesRead == 0)
                {
                    zeroReadCount++;

                    if (!pipeServer_IsConnected(pipeStream))
                        return null;

                    if (zeroReadCount >= maxZeroReads)
                        return null;

                    continue;
                }

                zeroReadCount = 0;
                Array.Copy(buffer, 0, frameData, offset, bytesRead);
                offset += bytesRead;
            }

            return frameData;
        }

        /// <summary>
        /// 从字节数组读取 BigEndian 编码的 32 位有符号整数。
        /// </summary>
        private static int ReadInt32BigEndian(byte[] buffer, int offset)
        {
            if (BitConverter.IsLittleEndian)
            {
                return (buffer[offset] << 24)
                    | (buffer[offset + 1] << 16)
                    | (buffer[offset + 2] << 8)
                    | buffer[offset + 3];
            }
            else
            {
                return BitConverter.ToInt32(buffer, offset);
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
