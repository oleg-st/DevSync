using System.Collections.Generic;
using BenchmarkDotNet.Attributes;
using DevSyncLib;

namespace DevSyncBenchmark
{
    public class FileMaskListBenchmark
    {
        private FileMaskList _fileList;

        [GlobalSetup]
        public void Setup()
        {
            _fileList = new FileMaskList();
            _fileList.SetList(new List<string>
            {
                "*.txt",
                "/var",
                "dist",
                "!/var/log",
                "start*end.cpp",
                "~*",
                "*.tmp",
                "*.pyc",
                "*.swp",
                ".git",
                "CVS",
            });
        }

        [Benchmark]
        public void Masks()
        {
            // txt
            _fileList.IsMatch("test.txt");
            _fileList.IsMatch("home/test.txt");
            _fileList.IsMatch("test.txt1");

            // var
            _fileList.IsMatch("var/www");
            _fileList.IsMatch("www/var");

            // dist
            _fileList.IsMatch("home/www/dist");
            _fileList.IsMatch("dist");
            _fileList.IsMatch("distX");

            // negative
            _fileList.IsMatch("var/log");
            _fileList.IsMatch("var/log1");
            _fileList.IsMatch("var/log/test");

            // wildcard
            _fileList.IsMatch("start_middle_end.cpp");
            _fileList.IsMatch("start_middle.cpp");
        }
    }
}
