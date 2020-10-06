using DevSyncLib;
using DevSyncLib.Command;
using System;
using System.IO;
using Xunit;

namespace DevSyncTest.Command
{
    public class ReaderWriterTest
    {
        [Fact]
        public void TestBool()
        {
            TestReadWrite((writer, value) => writer.WriteBool(value), reader => reader.ReadBool(), true, false);
        }

        [Fact]
        public void TestByte()
        {
            TestReadWrite<byte>((writer, value) => writer.WriteByte(value), reader => reader.ReadByte(), byte.MinValue, 128, byte.MaxValue);
        }

        [Fact]
        public void TestDateTIme()
        {
            TestReadWrite((writer, value) => writer.WriteDateTime(value), reader => reader.ReadDateTime(), DateTime.Now);
        }

        [Fact]
        public void TestInt()
        {
            TestReadWrite((writer, value) => writer.WriteInt(value), reader => reader.ReadInt(), 0, int.MaxValue, int.MinValue);
        }

        [Fact]
        public void TestInt16()
        {
            TestReadWrite<short>((writer, value) => writer.WriteInt16(value), reader => reader.ReadInt16(), 0, short.MaxValue, short.MinValue);
        }

        [Fact]
        public void TestLong()
        {
            TestReadWrite((writer, value) => writer.WriteLong(value), reader => reader.ReadLong(), 0, long.MaxValue, long.MinValue);
        }

        [Fact]
        public void TestString()
        {
            TestReadWrite((writer, value) => writer.WriteString(value), reader => reader.ReadString(), "", "Text");
        }

        [Fact]
        public void TestFsChange()
        {
            TestReadWrite((writer, value) => writer.WriteFsChange(value), reader => reader.ReadFsChange(),
                FsChange.Empty,
                new FsChange(FsChangeType.Change, "path") { LastWriteTime = DateTime.Now, Length = 1024 },
                new FsChange(FsChangeType.Remove, "path"),
                new FsChange(FsChangeType.Rename, "path") { OldPath = "oldPath" }
            );
        }

        [Fact]
        public void TestFsEntry()
        {
            TestReadWrite((writer, value) => writer.WriteFsEntry(value), reader => reader.ReadFsEntry(),
                FsEntry.Empty,
                new FsEntry { Path = "path", LastWriteTime = DateTime.Now, Length = 1024 },
                new FsEntry { Path = "path", LastWriteTime = DateTime.Now, Length = -1 }
            );
        }

        [Fact]
        public void TestFsChangeResult()
        {
            TestReadWrite((writer, value) => writer.WriteFsChangeResult(value), reader => reader.ReadFsChangeResult(),
                FsChangeResult.Empty,
                new FsChangeResult { ChangeType = FsChangeType.Change, ErrorMessage = null, Path = "path", ResultCode = FsChangeResultCode.Ok },
                new FsChangeResult { ChangeType = FsChangeType.Rename, ErrorMessage = "Test", Path = "path", ResultCode = FsChangeResultCode.Error },
                new FsChangeResult { ChangeType = FsChangeType.Remove, ErrorMessage = "Test", Path = "path", ResultCode = FsChangeResultCode.SenderError }
            );
        }

        private void TestReadWrite<T>(Action<Writer, T> writeFunc, Func<Reader, T> readFunc, params T[] values)
        {
            var memoryStream = new MemoryStream();
            var writer = new Writer(memoryStream, LoggerHelper.DummyLogger);
            var reader = new Reader(memoryStream, LoggerHelper.DummyLogger);
            foreach (var value in values)
            {
                memoryStream.Seek(0, SeekOrigin.Begin);
                writeFunc(writer, value);
                memoryStream.Seek(0, SeekOrigin.Begin);
                Assert.Equal(readFunc(reader), value);
            }
        }
    }
}
