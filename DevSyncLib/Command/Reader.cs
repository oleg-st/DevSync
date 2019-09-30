using System;
using System.IO;
using System.Text;
using DevSyncLib.Logger;

namespace DevSyncLib.Command
{
    public class Reader
    {
        private readonly ILogger _logger;

        protected BinaryReader BinaryReader;
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
            var changeType = (FsChangeType) ReadByte();
            if (changeType == FsChangeType.EmptyMarker)
            {
                return FsChange.Empty;
            }
            var fsChange = new FsChange
            {
                ChangeType = changeType,
                FsEntry = ReadFsEntry()
            };
            fsChange.OldFsEntry = fsChange.ChangeType == FsChangeType.Rename ? ReadFsEntry() : FsEntry.Empty;
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
            const int bufferLength = 65536;
            Span<byte> buffer = stackalloc byte[bufferLength];

            string tempPath = null;
            FileStream fs = null;
            bool bodyReadSuccess = false;
            try
            {
                long written = 0;
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
                        var read = BinaryReader.Read(buffer.Slice(0, Math.Min(bufferLength, remain)));
                        if (read <= 0)
                        {
                            throw new EndOfStreamException($"Premature end of stream {remain}, {chunkSize}, {read})");
                        }

                        if (written == 0)
                        {
                            var directoryName = Path.GetDirectoryName(path);
                            Directory.CreateDirectory(directoryName);
                            tempPath = Path.Combine(directoryName,
                                "." + Path.GetFileName(path) + "." + Path.GetRandomFileName());
                            fs = new FileStream(tempPath, FileMode.CreateNew, FileAccess.Write, FileShare.Read);
                        }

                        fs?.Write(buffer.Slice(0, read));
                        written += read;
                        remain -= read;
                    }
                } while (chunkSize > 0);

                bodyReadSuccess = written == fsChange.FsEntry.Length;
                return bodyReadSuccess;
            }
            catch (Exception)
            {
                SkipFsChangeBody();
                throw;
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
                            File.SetLastWriteTime(tempPath, fsChange.FsEntry.LastWriteTime);
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
            const int bufferLength = 65536;
            Span<byte> buffer = stackalloc byte[bufferLength];

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
                    var read = BinaryReader.Read(buffer.Slice(0, Math.Min(bufferLength, remain)));
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
