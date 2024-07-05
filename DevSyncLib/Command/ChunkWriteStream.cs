using DevSyncLib.Command.Compression;
using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace DevSyncLib.Command;

public class ChunkWriteStream : Stream
{
    // max chunk size
    public const int ChunkSize = 1024 * 1024;
    private readonly byte[] _chunkBytes = new byte[ChunkSize];
    private int _chunkLength;
    private readonly ChunkWriteStreamFlusher _flusher;

    public ChunkWriteStream(Stream stream, ICompression compression)
    {
        _flusher = new ChunkWriteStreamFlusher(stream, compression);
        _flusher.Start();
    }

    public class ChunkWriteStreamFlusher
    {
        // 4 bytes (int32) for chunk length
        private const int LengthSize = 4;

        private readonly Stream _stream;
        private readonly ICompression? _compression;
        private volatile bool _needToQuit, _needToFlush;
        private Exception? _flushException;

        private readonly ManualResetEvent _hasWorkEvent = new(false);
        private readonly ManualResetEvent _noFlushEvent = new(true);

        private int _chunkLength;
        private readonly byte[] _chunkBytes = new byte[ChunkSize + LengthSize];
        private readonly byte[]? _chunkCompressedBytes;
        private SlimStopwatch _flushStopwatch;
        // flush every 500ms (so agent would have some data to process instead of waiting)
        public const int FlushTimeout = 500;
        // compress chunk if length is more than
        public const int CompressionThreshold = 1024;

        public bool TimedOut => _flushStopwatch is { IsRunning: true, ElapsedMilliseconds: >= FlushTimeout };

        public ChunkWriteStreamFlusher(Stream stream, ICompression compression)
        {
            _stream = stream;
            _compression = compression;
            _flushStopwatch = SlimStopwatch.Create();

            if (_compression != null)
            {
                _chunkCompressedBytes = new byte[ChunkSize + LengthSize];
            }
        }

        private void UpdateHasWork()
        {
            lock (this)
            {
                if (_needToFlush || _needToQuit)
                {
                    _hasWorkEvent.Set();
                }
                else
                {
                    _hasWorkEvent.Reset();
                }

                if (_needToFlush)
                {
                    _noFlushEvent.Reset();
                }
                else
                {
                    _noFlushEvent.Set();
                }
            }
        }

        private void WaitForFlush()
        {
            _noFlushEvent.WaitOne();
            // lock chunk
            lock (_chunkBytes)
            {
                if (_flushException != null)
                {
                    throw _flushException;
                }
            }
        }

        protected void WriteChunk(byte[] buffer, int length, bool compressed)
        {
            buffer[0] = (byte)(length & 0xFF);
            buffer[1] = (byte)((length >> 8) & 0xFF);
            buffer[2] = (byte)((length >> 16) & 0xFF);
            buffer[3] = (byte)((length >> 24) & 0x7F);
            if (compressed)
            {
                buffer[3] |= 0x80;
            }
            // write length and data
            try
            {
                _stream.Write(buffer, 0, LengthSize + length);
                _stream.Flush();
            }
            catch (EndOfStreamException)
            {
                throw new EndOfStreamException("Connection closed");
            }
        }

        protected bool TryCompress(out int written)
        {
            Debug.Assert(_compression != null);
            Debug.Assert(_chunkCompressedBytes != null);
            return _compression.TryCompress(_chunkBytes, LengthSize, _chunkLength,
                _chunkCompressedBytes, LengthSize, ChunkSize, out written);
        }

        private void DoWork()
        {
            // lock chunk
            lock (_chunkBytes)
            {
                if (!_needToFlush)
                {
                    return;
                }

                try
                {
                    if (_compression != null && _chunkLength >= CompressionThreshold &&
                        TryCompress(out var written))
                    {
                        Debug.Assert(_chunkCompressedBytes != null);
                        WriteChunk(_chunkCompressedBytes, written, true);
                    }
                    else
                    {
                        WriteChunk(_chunkBytes, _chunkLength, false);
                    }
                }
                catch (Exception exception)
                {
                    _flushException = exception;
                }

                _flushStopwatch.Start();
                _needToFlush = false;
            }
            UpdateHasWork();
        }

        protected void Run()
        {
            while (!_needToQuit)
            {
                _hasWorkEvent.WaitOne();
                DoWork();
            }
        }

        public void Start()
        {
            Task.Factory.StartNew(Run, TaskCreationOptions.LongRunning);
        }

        public void Stop()
        {
            _needToQuit = true;
            // wait for current flush
            WaitForFlush();
            UpdateHasWork();
        }

        public void Flush(byte[] data, int offset, int length)
        {
            if (length == 0 || _needToQuit)
            {
                return;
            }

            // block other Flushes
            // wait for current flush
            WaitForFlush();
            // start new flush
            // lock chunk
            lock (_chunkBytes)
            {
                _flushException = null;
                _chunkLength = length;
                Buffer.BlockCopy(data, offset, _chunkBytes, LengthSize, _chunkLength);
                _needToFlush = true;
            }
            UpdateHasWork();
        }

        public void Wait()
        {
            // wait for current flush
            WaitForFlush();
        }
    }

    public override void Flush()
    {
        FlushChunk();
        _flusher.Wait();
    }

    protected void FlushChunk()
    {
        if (_chunkLength <= 0)
        {
            return;
        }

        _flusher.Flush(_chunkBytes, 0, _chunkLength);
        _chunkLength = 0;
    }

    public override void Close()
    {
        FlushChunk();
        _flusher.Stop();
    }

    public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();

    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

    public override void SetLength(long value) => throw new NotSupportedException();

    public override void Write(byte[] buffer, int offset, int count)
    {
        while (count > 0)
        {
            var toCopy = Math.Min(count, ChunkSize - _chunkLength);
            Buffer.BlockCopy(buffer, offset, _chunkBytes, _chunkLength, toCopy);

            _chunkLength += toCopy;
            offset += toCopy;
            count -= toCopy;

            if (_chunkLength >= ChunkSize)
            {
                FlushChunk();
            }
        }

        if (_chunkLength > 0 && _flusher.TimedOut)
        {
            FlushChunk();
        }
    }

    public override bool CanRead => false;
    public override bool CanSeek => false;
    public override bool CanWrite => true;
    public override long Length => throw new NotSupportedException();
    public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
}