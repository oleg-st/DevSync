using System;
using System.IO;

namespace DevSyncLib.Command.Compression
{
    public class LimitedBufferStream : Stream
    {
        private readonly byte[] _buffer;
        private readonly int _offset;
        private int _length;
        private int _position;
        public bool IsLimitReached { get; private set; }

        public LimitedBufferStream(byte[] buffer, int offset, int length)
        {
            _buffer = buffer;
            _offset = offset;
            _length = length;
            _position = 0;
        }

        public override void Flush()
        {
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            int read = Math.Min(count, _length - _position);
            Buffer.BlockCopy(_buffer, _offset + _position, buffer, offset, read);
            _position += read;
            return read;
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            switch (origin)
            {
                case SeekOrigin.Begin:
                    _position = (int)offset;
                    break;
                case SeekOrigin.Current:
                    _position += (int)offset;
                    break;
                case SeekOrigin.End:
                    _position = _length + (int)offset;
                    break;
            }

            return _position;
        }

        public override void SetLength(long value)
        {
            _length = Math.Min(_length, (int)value);
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            if (_position + count > _length)
            {
                IsLimitReached = true;
                return;
            }

            Buffer.BlockCopy(buffer, offset, _buffer, _offset + _position, count);
            _position += count;
        }

        public override bool CanRead => true;
        public override bool CanSeek => true;
        public override bool CanWrite => true;
        public override long Length => _length;
        public override long Position { get => _position; set => _position = (int)value; }
    }
}
