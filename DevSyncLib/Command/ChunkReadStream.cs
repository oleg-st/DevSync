﻿using DevSyncLib.Command.Compression;
using System;
using System.IO;

namespace DevSyncLib.Command
{
    public class ChunkReadStream : Stream
    {
        private readonly Stream _baseStream;
        private readonly ICompression _compression;
        public const int ChunkSize = ChunkWriteStream.ChunkSize;
        private readonly byte[] _chunkBytes = new byte[ChunkSize];
        private readonly byte[] _chunkCompressedBytes = new byte[ChunkSize];
        private int _chunkPosition, _chunkLength;
        private readonly byte[] _lengthBytes = new byte[4];
        public ChunkReadStream(Stream baseStream, ICompression compression)
        {
            _baseStream = baseStream;
            _compression = compression;
        }

        public override void Flush()
        {
        }

        protected void ReadFill(byte[] buffer, int offset, int count)
        {
            while (count > 0)
            {
                var read = _baseStream.Read(buffer, offset, count);
                if (read == 0)
                {
                    throw new EndOfStreamException("Connection closed");
                }

                offset += read;
                count -= read;
            }
        }

        protected void TryDecompress()
        {
            ReadFill(_chunkCompressedBytes, 0, _chunkLength);
            if (!_compression.TryDecompress(
                _chunkCompressedBytes, 0, _chunkLength,
                _chunkBytes, 0, ChunkSize, out var written))
            {
                throw new SyncException("Chunk decompress failed");
            }

            _chunkLength = written;
        }

        protected void ReadChunk()
        {
            ReadFill(_lengthBytes, 0, _lengthBytes.Length);
            bool compressed = (_lengthBytes[3] & 0x80) != 0;
            _chunkLength = _lengthBytes[0] | (_lengthBytes[1] << 8) | (_lengthBytes[2] << 16) | ((_lengthBytes[3] & 0x7F) << 24);
            if (_chunkLength > ChunkSize)
            {
                throw new SyncException($"Chunk is too long {_chunkLength}");
            }

            if (compressed)
            {
                TryDecompress();
            }
            else
            {
                ReadFill(_chunkBytes, 0, _chunkLength);
            }
            _chunkPosition = 0;
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            int totalRead = 0;
            while (count > 0)
            {
                if (_chunkPosition >= _chunkLength)
                {
                    ReadChunk();
                }
                var toCopy = Math.Min(count, _chunkLength - _chunkPosition);

                Buffer.BlockCopy(_chunkBytes, _chunkPosition, buffer, offset, toCopy);
                _chunkPosition += toCopy;
                offset += toCopy;
                count -= toCopy;
                totalRead += toCopy;
            }

            return totalRead;
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
            throw new NotSupportedException();
        }

        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException();
        public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
    }
}
