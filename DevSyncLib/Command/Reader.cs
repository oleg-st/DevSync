using DevSyncLib.Logger;
using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;

namespace DevSyncLib.Command
{
    public class Reader
    {
        private readonly ILogger _logger;

        protected BinaryReader BinaryReader;

        private const int BufferLength = 65536;
        private readonly byte[] _buffer = new byte[BufferLength];

        // detect shebang (#!) to make file executable
        private static readonly bool PlatformHasChmod = !RuntimeInformation.IsOSPlatform(OSPlatform.Windows);
        private static readonly byte[] ShebangBytes = { (byte)'#', (byte)'!' };
        private static readonly int ShebangLength = ShebangBytes.Length;

        public Reader(Stream stream, ILogger logger)
        {
            _logger = logger;
            BinaryReader = new BinaryReader(stream, Encoding.UTF8);
        }

        public int ReadInt()
        {
            return BinaryReader.ReadInt32();
        }

        public short ReadInt16()
        {
            return BinaryReader.ReadInt16();
        }
        public bool ReadBool()
        {
            return BinaryReader.ReadBoolean();
        }

        public byte ReadByte()
        {
            return BinaryReader.ReadByte();
        }

        public string ReadString()
        {
            return BinaryReader.ReadString();
        }

        public long ReadLong()
        {
            return BinaryReader.ReadInt64();
        }

        public DateTime ReadDateTime()
        {
            return DateTime.FromFileTime(BinaryReader.ReadInt64());
        }

        public FsEntry ReadFsEntry()
        {
            var path = ReadString();
            if (string.IsNullOrEmpty(path))
            {
                return FsEntry.Empty;
            }

            return new FsEntry
            {
                Path = path,
                Length = ReadLong(),
                LastWriteTime = ReadDateTime(),
            };
        }

        public FsChange ReadFsChange()
        {
            var changeType = (FsChangeType)ReadByte();
            if (changeType == FsChangeType.EmptyMarker)
            {
                return FsChange.Empty;
            }

            var fsChange = new FsChange(changeType, ReadString());
            switch (fsChange.ChangeType)
            {
                case FsChangeType.Change:
                    fsChange.Length = ReadLong();
                    fsChange.IsDirectory = fsChange.Length == -1;
                    fsChange.LastWriteTime = ReadDateTime();
                    break;
                case FsChangeType.Rename:
                    fsChange.OldPath = ReadString();
                    break;
            }
            return fsChange;
        }

        public FsChangeResult ReadFsChangeResult()
        {
            var changeType = (FsChangeType)ReadByte();
            if (changeType == FsChangeType.EmptyMarker)
            {
                return FsChangeResult.Empty;
            }

            var fsChangeResult = new FsChangeResult
            {
                ChangeType = changeType,
                Path = ReadString(),
                ResultCode = (FsChangeResultCode)ReadByte()
            };

            if (fsChangeResult.ResultCode != FsChangeResultCode.Ok)
            {
                fsChangeResult.ErrorMessage = ReadString();
            }

            return fsChangeResult;
        }

        public bool ReadFsChangeBody(string path, FsChange fsChange)
        {
            var shebangPosition = 0;
            var makeExecutable = false;

            string tempPath = null;
            FileStream fs = null;
            bool bodyReadSuccess = false;
            try
            {
                long written = 0;
                try
                {
                    int chunkSize;
                    do
                    {
                        chunkSize = ReadInt();
                        if (chunkSize < 0)
                        {
                            // error occurred in sender
                            return false;
                        }

                        var remain = chunkSize;
                        while (remain > 0)
                        {
                            var read = BinaryReader.Read(_buffer, 0, Math.Min(BufferLength, remain));
                            if (read <= 0)
                            {
                                throw new EndOfStreamException(
                                    $"Premature end of stream {remain}, {chunkSize}, {read})");
                            }

                            if (written == 0)
                            {
                                var directoryName = Path.GetDirectoryName(path);
                                Directory.CreateDirectory(directoryName);
                                tempPath = Path.Combine(directoryName,
                                    "." + Path.GetFileName(path) + "." + Path.GetRandomFileName());
                                fs = new FileStream(tempPath, FileMode.CreateNew, FileAccess.Write, FileShare.Read);
                            }

                            if (PlatformHasChmod && shebangPosition < ShebangLength)
                            {
                                for (var i = 0; i < read;)
                                {
                                    if (_buffer[i++] != ShebangBytes[shebangPosition++])
                                    {
                                        // no shebang
                                        shebangPosition = int.MaxValue;
                                        break;
                                    }

                                    if (shebangPosition == ShebangLength)
                                    {
                                        makeExecutable = true;
                                        break;
                                    }
                                }
                            }

                            fs?.Write(_buffer, 0, read);
                            written += read;
                            remain -= read;
                        }
                    } while (chunkSize > 0);
                }
                catch (Exception)
                {
                    SkipFsChangeBody();
                    throw;
                }

                bodyReadSuccess = written == fsChange.Length;
                if (bodyReadSuccess && makeExecutable)
                {
                    // 0755, rwxr-xr-x
                    fs.FChangeMode(0b111_101_101);
                }

                // create empty file
                if (bodyReadSuccess && fsChange.Length == 0)
                {
                    var directoryName = Path.GetDirectoryName(path);
                    Directory.CreateDirectory(directoryName);
                    using (new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read))
                    {
                    }
                    File.SetLastWriteTime(path, fsChange.LastWriteTime);
                }

                return bodyReadSuccess;
            }
            finally
            {
                if (fs != null)
                {
                    fs.Dispose();

                    if (bodyReadSuccess)
                    {
                        try
                        {
                            File.SetLastWriteTime(tempPath, fsChange.LastWriteTime);
                            File.Move(tempPath, path, true);
                        }
                        catch (Exception)
                        {
                            FsHelper.TryDeleteFile(tempPath);
                            throw;
                        }
                    }
                    else
                    {
                        FsHelper.TryDeleteFile(tempPath);
                    }
                }
            }
        }

        public void SkipFsChangeBody()
        {
            do
            {
                var chunkSize = ReadInt();
                if (chunkSize <= 0)
                {
                    return;
                }

                var remain = chunkSize;
                while (remain > 0)
                {
                    var read = BinaryReader.Read(_buffer, 0, Math.Min(BufferLength, remain));
                    if (read == 0)
                    {
                        throw new EndOfStreamException();
                    }
                    remain -= read;
                }
            } while (true);
        }
    }
}
