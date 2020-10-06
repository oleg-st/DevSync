using DevSyncLib;
using Xunit;

namespace DevSyncTest.Command
{
    public class FsEntryTest
    {
        [Fact]
        public void TestNormalizePath()
        {
            Assert.Equal("", FsEntry.NormalizePath("."));
            Assert.Equal("", FsEntry.NormalizePath("././././././////"));
            Assert.Equal("abc", FsEntry.NormalizePath("abc"));
            Assert.Equal("abc", FsEntry.NormalizePath(".\\abc"));
            Assert.Equal("..", FsEntry.NormalizePath(".."));
        }
    }
}
