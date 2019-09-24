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

        public bool ReadFsChangeBody(FileStream fs, long length)
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

                int totalRead = 0;
                int remain = chunkSize;

                while (remain > 0)
                {
                    int read = BinaryReader.Read(_buffer.Slice(totalRead, remain).Span);
                    if (read == 0)
                    {
                        throw new EndOfStreamException($"Premature end of stream ({totalRead}, {remain}, {chunkSize})");
                    }

                    totalRead += read;
                    remain -= read;
                }

                fs?.Write(_buffer.Slice(0, totalRead).Span);
                written += totalRead;
            } while (chunkSize > 0);

            return written == length;
        }
    }
}
