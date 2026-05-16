using System;
using System.IO;
using System.IO.Pipes;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;

namespace FFmpegRunner
{
    /// <summary>
    /// 流管道实现类。直接透传从命名管道读取的原始字节数据，不进行任何协议解析。
    /// 适用于需要直接处理 FFmpeg 原始输出的场景（如转码、录制等）。
    /// </summary>
    /// <remarks>
    /// 数据流：FFmpeg → NamedPipeServerStream → Channel (byte array) → DataReceived 事件 → 用户回调
    /// </remarks>
    public class StreamPipe : IPipeInterface
    {
        private NamedPipeServerStream? _pipeServer;
        private CancellationTokenSource? _cancelCts;
        private Task? _readTask;
        private Task? _consumerTask;
        private Channel<byte[]>? _channel;
        private bool _disposed;

        /// <summary>
        /// 获取当前管道的名称。
        /// </summary>
        public string PipeName { get; }

        /// <summary>
        /// 获取或设置管道数据缓冲区的容量（数据块数）。必须在 <see cref="Initialize"/> 之前设置。默认值 100。
        /// </summary>
        public int BufferCapacity { get; set; } = 100;

        /// <summary>
        /// 当管道读取到新的数据时触发。
        /// </summary>
        public event EventHandler<FrameEventArgs>? DataReceived;

        /// <summary>
        /// 初始化 <see cref="StreamPipe"/> 类的新实例。
        /// </summary>
        /// <param name="pipeName">管道名称。</param>
        /// <exception cref="ArgumentNullException">当 <paramref name="pipeName"/> 为 <c>null</c> 时抛出。</exception>
        public StreamPipe(string pipeName)
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
                65536,
                65536);

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
                try
                {
                    await pipeServer.WaitForConnectionAsync(token).ConfigureAwait(false);

                    var buffer = new byte[8192];

                    while (!token.IsCancellationRequested)
                    {
                        var bytesRead = await pipeServer.ReadAsync(
                            buffer, 0, buffer.Length, token).ConfigureAwait(false);

                        if (bytesRead == 0)
                        {
                            if (!pipeServer.IsConnected)
                                break;

                            continue;
                        }

                        var data = new byte[bytesRead];
                        Array.Copy(buffer, data, bytesRead);
                        await writer.WriteAsync(data, token).ConfigureAwait(false);
                    }
                }
                catch (OperationCanceledException)
                {
                }
                catch (IOException)
                {
                }
                finally
                {
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
        /// 触发 <see cref="DataReceived"/> 事件。
        /// </summary>
        /// <param name="data">从管道读取的数据。</param>
        protected virtual void OnDataReceived(byte[] data)
        {
            DataReceived?.Invoke(this, new FrameEventArgs(data, null));
        }
    }
}