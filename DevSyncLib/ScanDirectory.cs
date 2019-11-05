using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using DevSyncLib.Logger;

namespace DevSyncLib
{
    public class ScanDirectory
    {
        private readonly FileMaskList _excludeList;
        private readonly ILogger _logger;
        private readonly bool _withInfo;
        private readonly CancellationToken? _cancellationToken;

        public ScanDirectory(ILogger logger, FileMaskList excludeList, bool withInfo = true, CancellationToken? cancellationToken = null)
        {
            _logger = logger;
            _excludeList = excludeList;
            _withInfo = withInfo;
            _cancellationToken = cancellationToken;
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
                    _cancellationToken?.ThrowIfCancellationRequested();

                    var path = Path.Combine(relativePath, fsInfo.Name);
                    FsEntry fsEntry = FsEntry.Empty;

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

                        if (!fsEntry.IsEmpty)
                        {
                            yield return fsEntry;
                        }
                    }
                }
            }
        }
    }
}
