using System;
using System.IO;
using System.Text;

namespace DevSyncLib.Command
{
    public class Reader
    {
        protected BinaryReader BinaryReader;
        // same size as chunk size
        private const int BUFFER_LENGTH = ChunkWriteStream.ChunkSize;
        private Memory<byte> _buffer = new byte[BUFFER_LENGTH];

        public Reader(Stream stream)
        {
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
            return new FsEntry
            {
                Path = ReadString(),
                Length = ReadLong(),
                LastWriteTime = ReadDateTime(),
            };
        }

        public FsChange ReadFsChange()
        {
            var fsChange = new FsChange
            {
                ChangeType = (FsChangeType) ReadInt16(),
                FsEntry = ReadFsEntry()
            };
            fsChange.OldFsEntry = fsChange.ChangeType == FsChangeType.Rename ? ReadFsEntry() : null;
            return fsChange;
        }

        public FsChangeResult ReadFsChangeResult()
        {
            var fsChangeResult = new FsChangeResult
            {
                ChangeType = (FsChangeType)ReadByte(),
                Path = ReadString(),
                ResultCode = (FsChangeResultCode)ReadByte()
            };

            if (fsChangeResult.ResultCode != FsChangeResultCode.Ok)
            {
                fsChangeResult.Error = ReadString();
            }

            return fsChangeResult;
        }

        public bool ReadFsChangeBody(string path, FsChange fsChange)
        {
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

                    if (chunkSize > BUFFER_LENGTH)
                    {
                        throw new SyncException("Chunk is too long");
                    }

                    var totalRead = 0;
                    var remain = chunkSize;

                    while (remain > 0)
                    {
                        int read = BinaryReader.Read(_buffer.Slice(totalRead, remain).Span);
                        if (read == 0)
                        {
                            throw new EndOfStreamException(
                                $"Premature end of stream ({totalRead}, {remain}, {chunkSize})");
                        }

                        totalRead += read;
                        remain -= read;
                    }

                    if (written == 0)
                    {
                        var directoryName = Path.GetDirectoryName(path);
                        Directory.CreateDirectory(directoryName);
                        tempPath = Path.Combine(directoryName,
                            "." + Path.GetFileName(path) + "." + Path.GetRandomFileName());
                        fs = new FileStream(tempPath, FileMode.CreateNew, FileAccess.Write, FileShare.Read);
                    }

                    fs?.Write(_buffer.Slice(0, totalRead).Span);
                    written += totalRead;
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
            do
            {
                var chunkSize = ReadInt();
                if (chunkSize <= 0)
                {
                    return;
                }

                if (chunkSize > BUFFER_LENGTH)
                {
                    throw new SyncException("Chunk is too long");
                }

                var remain = chunkSize;
                while (remain > 0)
                {
                    var read = BinaryReader.Read(_buffer.Span);
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
