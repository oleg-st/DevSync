using DevSyncLib.Logger;
using System;
using System.IO;
using System.Text;

namespace DevSyncLib.Command
{
    public class Writer
    {
        private readonly ILogger _logger;

        protected BinaryWriter BinaryWriter;

        private const int BufferLength = 65536;
        private readonly byte[] _buffer = new byte[BufferLength];

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
            if (fsEntry.IsEmpty)
            {
                return;
            }

            WriteLong(fsEntry.Length);
            WriteDateTime(fsEntry.LastWriteTime);
        }

        public void WriteFsChange(FsChange fsChange)
        {
            WriteByte((byte)fsChange.ChangeType);
            if (fsChange.IsEmpty)
            {
                return;
            }
            WriteString(fsChange.Path);
            if (fsChange.IsChange)
            {
                WriteLong(fsChange.IsDirectory ? -1 : fsChange.Length);
                WriteDateTime(fsChange.LastWriteTime);
            }
            if (fsChange.IsRename)
            {
                WriteString(fsChange.OldPath);
            }
        }

        public void WriteFsChangeResult(FsChangeResult fsChangeResult)
        {
            WriteByte((byte)fsChangeResult.ChangeType);
            if (fsChangeResult.IsEmpty)
            {
                return;
            }
            WriteString(fsChangeResult.Path);
            WriteByte((byte)fsChangeResult.ResultCode);
            if (fsChangeResult.ResultCode != FsChangeResultCode.Ok)
            {
                WriteString(fsChangeResult.ErrorMessage);
            }
        }

        public bool WriteFsChangeBody(string filename, FsSenderChange fsSenderChange)
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
                        // file vanished
                        fsSenderChange.Vanished = true;
                    }
                    else
                    {
                        // something else
                        _logger.Log(ex.Message, LogLevel.Error);
                    }

                    // cannot read file (sender error)
                    WriteFsChange(fsSenderChange);
                    WriteInt(-1);
                    return false;
                }

                // length resolved
                fsSenderChange.Length = fs.Length;
                WriteFsChange(fsSenderChange);
                fsSenderChange.Opened = true;

                int read;
                do
                {
                    if (fsSenderChange.Expired)
                    {
                        // file change is expired -> stop
                        WriteInt(-1);
                        return false;
                    }

                    try
                    {
                        read = fs.Read(_buffer, 0, BufferLength);
                        if (read <= 0)
                        {
                            break;
                        }
                    }
                    catch (Exception ex)
                    {
                        // file read error (sender error)
                        _logger.Log(ex.Message, LogLevel.Error);
                        WriteInt(-1);
                        return false;
                    }

                    WriteInt(read);
                    BinaryWriter.Write(_buffer, 0, read);
                    written += read;
                } while (read == BufferLength);

                // check written
                if (written != fsSenderChange.Length)
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
