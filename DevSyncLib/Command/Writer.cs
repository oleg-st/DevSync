using System;
using System.IO;
using System.Text;
using DevSyncLib.Logger;

namespace DevSyncLib.Command
{
    public class Writer
    {
        private readonly ILogger _logger;

        protected BinaryWriter BinaryWriter;
        // same size as chunk size
        private const int BUFFER_LENGTH = ChunkWriteStream.ChunkSize;
        private Memory<byte> _buffer = new byte[BUFFER_LENGTH];
        public Writer(Stream stream, ILogger logger)
        {
            _logger = logger;
            BinaryWriter = new BinaryWriter(stream, Encoding.UTF8);
        }

        public void WriteInt(int value)
        {
            BinaryWriter.Write(value);
        }

        public void WriteInt16(short value)
        {
            BinaryWriter.Write(value);
        }

        public void WriteBool(bool value)
        {
            BinaryWriter.Write(value);
        }

        public void WriteByte(byte value)
        {
            BinaryWriter.Write(value);
        }

        public void WriteString(string value)
        {
            BinaryWriter.Write(value);
        }

        public void WriteLong(long value)
        {
            BinaryWriter.Write(value);
        }

        public void WriteDateTime(DateTime value)
        {
            BinaryWriter.Write(value.ToFileTime());
        }

        public void WriteFsEntry(FsEntry fsEntry)
        {
            WriteString(fsEntry.Path);
            WriteLong(fsEntry.Length);
            WriteDateTime(fsEntry.LastWriteTime);
        }

        public void WriteFsChange(FsChange fsChange)
        {
            WriteByte((byte)fsChange.ChangeType);
            if (fsChange.IsEnd)
            {
                return;
            }
            WriteFsEntry(fsChange.FsEntry);
            if (fsChange.ChangeType == FsChangeType.Rename)
            {
                WriteFsEntry(fsChange.OldFsEntry);
            }
        }

        public void WriteFsChangeResult(FsChangeResult fsChangeResult)
        {
            WriteByte((byte)fsChangeResult.ChangeType);
            if (fsChangeResult.IsEnd)
            {
                return;
            }
            WriteString(fsChangeResult.Path);
            WriteByte((byte)fsChangeResult.ResultCode);
            if (fsChangeResult.ResultCode != FsChangeResultCode.Ok)
            {
                WriteString(fsChangeResult.Error);
            }
        }

        public bool WriteFsChangeBody(string filename, FsChange fsChange)
        {
            long written = 0;
            FileStream fs = null;
            try
            {
                try
                {
                    fs = new FileStream(filename, FileMode.Open, FileAccess.Read,
                        FileShare.Delete | FileShare.ReadWrite);
                }
                catch (Exception ex)
                {
                    if (ex is FileNotFoundException || ex is DirectoryNotFoundException)
                    {
                        // file vanished -> ignore it
                        fsChange.Expired = true;
                    }
                    else
                    {
                        // something else
                        _logger.Log(ex.Message, LogLevel.Error);
                    }

                    // cannot read file (sender error)
                    WriteInt(-1);
                    return false;
                }

                // check length
                if (fs.Length != fsChange.FsEntry.Length)
                {
                    // file length mismatch
                    WriteInt(-1);
                    return false;
                }

                int read;
                do
                {
                    if (fsChange.Expired)
                    {
                        // file change is expired -> stop
                        WriteInt(-1);
                        return false;
                    }

                    try
                    {
                        read = fs.Read(_buffer.Span);
                        if (read <= 0)
                        {
                            break;
                        }
                    }
                    catch (Exception ex)
                    {
                        // file read error (sender error)
                        WriteInt(-1);
                        _logger.Log(ex.Message, LogLevel.Error);
                        return false;
                    }

                    WriteInt(read);
                    BinaryWriter.Write(_buffer.Slice(0, read).Span);
                    written += read;
                } while (read == BUFFER_LENGTH);

                // check written
                if (written != fsChange.FsEntry.Length)
                {
                    // file length mismatch
                    WriteInt(-1);
                    return false;
                }

                WriteInt(0);
                return true;
            }
            finally
            {
                fs?.Dispose();
            }
        }

        public void Flush()
        {
            BinaryWriter.Flush();
        }
    }
}
