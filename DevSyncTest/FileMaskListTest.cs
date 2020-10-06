using DevSyncLib;
using System.Collections.Generic;
using Xunit;

namespace DevSyncTest
{
    public class FileMaskListTest
    {
        [Fact]
        public void TestMasks()
        {
            var fileList = new FileMaskList();
            fileList.SetList(new List<string> { "*.txt", "/var", "dist", "!/var/log", "start*end.cpp" });

            // txt
            Assert.True(fileList.IsMatch("test.txt"));
            Assert.True(fileList.IsMatch("home/test.txt"));
            Assert.False(fileList.IsMatch("test.txt1"));

            // var
            Assert.True(fileList.IsMatch("var/www"));
            Assert.False(fileList.IsMatch("www/var"));

            // dist
            Assert.True(fileList.IsMatch("home/www/dist"));
            Assert.True(fileList.IsMatch("dist"));
            Assert.False(fileList.IsMatch("distX"));

            // negative
            Assert.False(fileList.IsMatch("var/log"));
            Assert.True(fileList.IsMatch("var/log1"));
            Assert.False(fileList.IsMatch("var/log/test"));

            // wildcard
            Assert.True(fileList.IsMatch("start_middle_end.cpp"));
            Assert.False(fileList.IsMatch("start_middle.cpp"));
        }
    }
}
