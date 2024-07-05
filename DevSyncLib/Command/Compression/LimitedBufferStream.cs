using System;
using System.IO;

namespace DevSyncLib.Command.Compression;

public class LimitedBufferStream(byte[] innerBuffer, int innerOffset, int innerLength) : Stream
{
    private int _position;
    public bool IsLimitReached { get; private set; }

    public override void Flush()
    {
    }

    public override int Read(byte[] buffer1, int offset1, int count)
    {
        int read = Math.Min(count, innerLength - _position);
        Buffer.BlockCopy(innerBuffer, innerOffset + _position, buffer1, offset1, read);
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
                _position = innerLength + (int)offset;
                break;
        }

        return _position;
    }

    public override void SetLength(long value)
    {
        innerLength = Math.Min(innerLength, (int)value);
    }

    public override void Write(byte[] buffer, int offset, int count)
    {
        if (_position + count > innerLength)
        {
            IsLimitReached = true;
            return;
        }

        Buffer.BlockCopy(buffer, offset, innerBuffer, innerOffset + _position, count);
        _position += count;
    }

    public override bool CanRead => true;
    public override bool CanSeek => true;
    public override bool CanWrite => true;
    public override long Length => innerLength;
    public override long Position { get => _position; set => _position = (int)value; }
}