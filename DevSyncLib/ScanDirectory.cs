using System;
using System.Collections.Generic;
using System.IO;
using DevSyncLib.Logger;

namespace DevSyncLib
{
    public class ScanDirectory
    {
        private readonly FileMaskList _excludeList;
        private readonly ILogger _logger;
        private readonly bool _withInfo;

        public ScanDirectory(ILogger logger, FileMaskList excludeList, bool withInfo = true)
        {
            _logger = logger;
            _excludeList = excludeList;
            _withInfo = withInfo;
        }

        public IEnumerable<FsEntry> ScanPath(string basePath, string relativePath = "")
        {
            IEnumerable<FileSystemInfo> fileSystemInfos = null;
            try
            {
                var fullPath = Path.Combine(basePath, relativePath);

                if (Directory.Exists(fullPath))
                {
                    var directoryInfo = new DirectoryInfo(fullPath);
                    fileSystemInfos = directoryInfo.EnumerateFileSystemInfos();
                }
            }
            catch (Exception ex)
            {
                _logger.Log($"Scan error {ex}", LogLevel.Warning);
            }

            if (fileSystemInfos != null)
            {
                foreach (var fsInfo in fileSystemInfos)
                {
                    var path = Path.Combine(relativePath, fsInfo.Name);
                    FsEntry fsEntry = null;

                    // skip symlinks and excludes
                    if ((fsInfo.Attributes & FileAttributes.ReparsePoint) == 0 &&
                        !_excludeList.IsMatch(FsEntry.NormalizePath(path)))
                    {
                        // scan children
                        if ((fsInfo.Attributes & FileAttributes.Directory) != 0)
                        {
                            foreach (var entry in ScanPath(basePath, path))
                            {
                                yield return entry;
                            }
                        }

                        try
                        {
                            fsEntry = FsEntry.FromFsInfo(path, fsInfo, _withInfo);
                        }
                        catch (Exception ex)
                        {
                            _logger.Log($"Scan error {ex}", LogLevel.Warning);
                        }

                        if (fsEntry != null)
                        {
                            yield return fsEntry;
                        }
                    }
                }
            }
        }
    }
}
