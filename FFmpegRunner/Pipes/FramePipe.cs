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
        private readonly List<byte[]> _pendingAu = new List<byte[]>();
        private bool _pendingAuHasVcl;

        public IFrameAnalyzer? FrameAnalyzer { get; set; } = new CompositeFrameAnalyzer();

        public string PipeName { get; }

        public int ReadTimeoutMilliseconds
        {
            get => _readTimeoutMs;
            set => _readTimeoutMs = Math.Max(0, value);
        }

        public int BufferCapacity { get; set; } = 100;

        public int MaxFrameSize
        {
            get => _maxFrameSize;
            set => _maxFrameSize = Math.Max(value, MinMaxFrameSize);
        }

        public event EventHandler<FrameEventArgs>? DataReceived;

        public FramePipe(string pipeName)
        {
            PipeName = pipeName ?? throw new ArgumentNullException(nameof(pipeName));
        }

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

                    while (!token.IsCancellationRequested)
                    {
                        if (!pipeServer_IsConnected(pipeServer))
                            break;

                        var bytesRead = await ReadWithTimeoutAsync(
                            pipeServer, sharedBuffer, token).ConfigureAwait(false);

                        if (bytesRead <= 0)
                            break;

                        for (int i = 0; i < bytesRead; i++)
                            _readBuffer.Add(sharedBuffer[i]);

                        if (_readBuffer.Count > _maxFrameSize)
                        {
                            _readBuffer.Clear();
                            continue;
                        }

                        ProcessBuffer(writer, token);
                    }

                    FlushPendingAu(writer, token);
                }
                catch (OperationCanceledException)
                {
                    FlushPendingAu(writer, token);
                }
                catch (IOException)
                {
                    FlushPendingAu(writer, token);
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
            _pendingAu.Clear();
            _pendingAuHasVcl = false;
            _readTask = null;
            _consumerTask = null;
            _cancelCts = null;
            _channel = null;
        }

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

        private void ProcessBuffer(ChannelWriter<byte[]?> writer, CancellationToken token)
        {
            while (true)
            {
                var mjpegFrame = ExtractMjpegFrame(_readBuffer);
                if (mjpegFrame != null)
                {
                    FlushPendingAu(writer, token);
                    writer.TryWrite(mjpegFrame);
                    continue;
                }

                var nalUnit = ExtractSingleNalUnit(_readBuffer);
                if (nalUnit == null)
                    break;

                bool isVcl = IsVclNalUnit(nalUnit);
                bool isBoundary = IsAuBoundary(nalUnit);

                if (_pendingAuHasVcl && (isBoundary || isVcl))
                {
                    var au = MergeNalUnits(_pendingAu);
                    writer.TryWrite(au);
                    _pendingAu.Clear();
                    _pendingAuHasVcl = false;
                }

                _pendingAu.Add(nalUnit);

                if (isVcl)
                {
                    _pendingAuHasVcl = true;
                }
            }
        }

        private void FlushPendingAu(ChannelWriter<byte[]?> writer, CancellationToken token)
        {
            if (_pendingAu.Count > 0)
            {
                var au = MergeNalUnits(_pendingAu);
                writer.TryWrite(au);
                _pendingAu.Clear();
                _pendingAuHasVcl = false;
            }
        }

        private static byte[]? ExtractSingleNalUnit(List<byte> buffer)
        {
            int firstSC = FindAnnexBStartCode(buffer, 0);
            if (firstSC < 0)
                return null;

            if (firstSC > 0)
            {
                buffer.RemoveRange(0, firstSC);
            }

            int secondSC = FindAnnexBStartCode(buffer, 1);
            if (secondSC < 0)
                return null;

            var nalUnit = buffer.GetRange(0, secondSC).ToArray();
            buffer.RemoveRange(0, secondSC);
            return nalUnit;
        }

        private static int FindAnnexBStartCode(List<byte> buffer, int startIndex)
        {
            for (int i = startIndex; i < buffer.Count - 2; i++)
            {
                if (IsAnnexBStartCode(buffer, i))
                    return i;
            }
            return -1;
        }

        private static bool IsAnnexBStartCode(List<byte> buffer, int index)
        {
            if (index + 3 < buffer.Count &&
                buffer[index] == 0x00 && buffer[index + 1] == 0x00 &&
                buffer[index + 2] == 0x00 && buffer[index + 3] == 0x01)
                return true;

            if (index + 2 < buffer.Count &&
                (index <= 0 || buffer[index - 1] != 0x00) &&
                buffer[index] == 0x00 && buffer[index + 1] == 0x00 && buffer[index + 2] == 0x01)
                return true;

            return false;
        }

        private static int GetNalUnitTypeByte(byte[] nalUnit)
        {
            for (int i = 0; i < nalUnit.Length - 2; i++)
            {
                if (IsStartCodeAt(nalUnit, i, out int scLen))
                    return nalUnit[i + scLen];
            }
            return -1;
        }

        private static bool IsStartCodeAt(byte[] data, int index, out int scLen)
        {
            scLen = 0;

            if (index + 3 < data.Length &&
                data[index] == 0x00 && data[index + 1] == 0x00 &&
                data[index + 2] == 0x00 && data[index + 3] == 0x01)
            {
                scLen = 4;
                return true;
            }

            if (index + 2 < data.Length &&
                data[index] == 0x00 && data[index + 1] == 0x00 && data[index + 2] == 0x01)
            {
                if (index <= 0 || data[index - 1] != 0x00)
                {
                    scLen = 3;
                    return true;
                }
            }

            return false;
        }

        private static bool IsVclNalUnit(byte[] nalUnit)
        {
            int typeByte = GetNalUnitTypeByte(nalUnit);
            if (typeByte < 0)
                return false;

            int h264Type = typeByte & 0x1F;
            int h265Type = (typeByte >> 1) & 0x3F;

            bool isH264Vcl = h264Type >= 1 && h264Type <= 5;
            bool isH265Vcl = h265Type <= 15;
            bool isH265Irap = h265Type >= 16 && h265Type <= 21;

            return isH264Vcl || isH265Vcl || isH265Irap;
        }

        private static bool IsAuBoundary(byte[] nalUnit)
        {
            int typeByte = GetNalUnitTypeByte(nalUnit);
            if (typeByte < 0)
                return false;

            if (typeByte == 0x67 || typeByte == 0x69)
                return true;

            if (typeByte == 0x40 || typeByte == 0x46)
                return true;

            return false;
        }

        private static byte[] MergeNalUnits(List<byte[]> nalUnits)
        {
            if (nalUnits.Count == 0)
                return Array.Empty<byte>();

            if (nalUnits.Count == 1)
                return nalUnits[0];

            int totalLength = 0;
            for (int i = 0; i < nalUnits.Count; i++)
                totalLength += nalUnits[i].Length;

            var result = new byte[totalLength];
            int offset = 0;
            for (int i = 0; i < nalUnits.Count; i++)
            {
                Buffer.BlockCopy(nalUnits[i], 0, result, offset, nalUnits[i].Length);
                offset += nalUnits[i].Length;
            }

            return result;
        }

        private static byte[]? ExtractMjpegFrame(List<byte> buffer)
        {
            int soiIndex = -1;
            for (int i = 0; i < buffer.Count - 1; i++)
            {
                if (buffer[i] == 0xFF && buffer[i + 1] == 0xD8)
                {
                    soiIndex = i;
                    break;
                }
            }

            if (soiIndex < 0)
                return null;

            if (soiIndex > 0)
            {
                buffer.RemoveRange(0, soiIndex);
            }

            for (int j = 2; j < buffer.Count - 1; j++)
            {
                if (buffer[j] == 0xFF && buffer[j + 1] == 0xD9)
                {
                    var frame = buffer.GetRange(0, j + 2).ToArray();
                    buffer.RemoveRange(0, j + 2);
                    return frame;
                }
            }

            return null;
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