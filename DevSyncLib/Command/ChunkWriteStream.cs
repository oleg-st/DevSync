using System;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using DevSyncLib.Command.Compression;

namespace DevSyncLib.Command
{
    public class ChunkWriteStream : Stream
    {
        private readonly Stream _baseStream;

        private readonly ICompress _compress;

        // max chunk size
        public const int ChunkSize = 1024 * 1024;
        // compress chunk if length is more than
        public const int CompressionThreshold = 1024;
        // flush every 500ms (so agent would have some data to process instead of waiting)
        public const int FlushTimeout = 500;
        // 4 bytes (int32) for chunk length
        private const int LENGTH_SIZE = 4;
        private Memory<byte> _chunkMemory = new byte[ChunkSize + LENGTH_SIZE];
        private Memory<byte> _chunkCompressedMemory = new byte[ChunkSize + LENGTH_SIZE];
        private int _chunkLength;
        private Stopwatch _flushStopwatch;

        public ChunkWriteStream(Stream baseStream, ICompress compress)
        {
            _baseStream = baseStream;
            _compress = compress;
        }

        public override void Flush()
        {
            FlushChunk();
        }

        protected void WriteChunk(Span<byte> span, int length, bool compressed)
        {
            span[0] = (byte)(length & 0xFF);
            span[1] = (byte)((length >> 8) & 0xFF);
            span[2] = (byte)((length >> 16) & 0xFF);
            span[3] = (byte)((length >> 24) & 0x7F);
            if (compressed)
            {
                span[3] |= 0x80;
            }
            // write length and data
            try
            {
                _baseStream.Write(span.Slice(0, LENGTH_SIZE + length));
                _baseStream.Flush();
            }
            catch (EndOfStreamException)
            {
                throw new EndOfStreamException("Connection closed");
            }
        }

        protected bool TryCompress(out int written)
        {
            // TODO: tune quality and window
            return _compress.TryCompress(_chunkMemory.Slice(LENGTH_SIZE, _chunkLength).Span,
                _chunkCompressedMemory.Slice( LENGTH_SIZE, ChunkSize).Span, out written);
        }

        protected void FlushChunk()
        {
            if (_chunkLength > 0)
            {
                if (_chunkLength >= CompressionThreshold && TryCompress(out var written))
                {
                    WriteChunk(_chunkCompressedMemory.Span, written, true);
                }
                else
                {
                    WriteChunk(_chunkMemory.Span, _chunkLength, false);
                }

                _chunkLength = 0;
                _flushStopwatch = Stopwatch.StartNew();
            }
        }

        public override void Close()
        {
            FlushChunk();
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            throw new NotSupportedException();
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            throw new NotSupportedException();
        }

        public override void SetLength(long value)
        {
            throw new NotSupportedException();
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            Write(new Span<byte>(buffer, offset, count));
        }

        public override void Write(ReadOnlySpan<byte> buffer)
        {
            int offset = 0;
            int count = buffer.Length;
            while (count > 0)
            {
                var toCopy = Math.Min(count, ChunkSize - _chunkLength);
                buffer.Slice( offset, toCopy).CopyTo(_chunkMemory.Slice(LENGTH_SIZE + _chunkLength, toCopy).Span);
                _chunkLength += toCopy;
                offset += toCopy;
                count -= toCopy;

                if (_chunkLength >= ChunkSize)
                {
                    FlushChunk();
                }
            }

            if (_chunkLength > 0 && _flushStopwatch != null && _flushStopwatch.ElapsedMilliseconds >= FlushTimeout)
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
}
