using System;
using System.Collections.Generic;
using System.IO;
using DevSyncLib.Logger;

namespace DevSyncLib
{
    public class ScanDirectory
    {
        private Dictionary<string, FsEntry> _fileList;
        private ExcludeList _excludeList;
        private readonly ILogger _logger;

        public ScanDirectory(ILogger logger)
        {
            _logger = logger;
            _fileList = new Dictionary<string, FsEntry>();
        }

        private void ScanPath(string basePath, string relativePath)
        {
            try
            {
                var fullPath = Path.Combine(basePath, relativePath);

                if (!Directory.Exists(fullPath))
                {
                    return;
                }

                var di = new DirectoryInfo(fullPath);
                foreach (var file in di.GetFiles())
                {
                    var path = Path.Combine(relativePath, file.Name);
                    // skip symlinks and excludes
                    if ((file.Attributes & FileAttributes.ReparsePoint) == 0 && !_excludeList.IsExcluded(FsEntry.NormalizePath(path)))
                    {
                        try
                        {
                            _fileList.Add(path, FsEntry.FromFileInfo(path, file));
                        }
                        catch (Exception ex)
                        {
                            // TODO: ignored errors
                            _logger.Log($"Scan error {ex}", LogLevel.Warning);
                        }
                    }
                }

                foreach (var dir in di.GetDirectories())
                {
                    var path = Path.Combine(relativePath, dir.Name);
                    // skip symlinks and excludes
                    if ((dir.Attributes & FileAttributes.ReparsePoint) == 0 && !_excludeList.IsExcluded(FsEntry.NormalizePath(path)))
                    {
                        ScanPath(basePath, path);
                        try
                        {
                            _fileList.Add(path, FsEntry.FromDirectoryInfo(path, dir));
                        }
                        catch (Exception ex)
                        {
                            // TODO: ignored errors
                            _logger.Log($"Scan error {ex}", LogLevel.Warning);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                // TODO: ignored errors
                _logger.Log($"Scan error {ex}", LogLevel.Warning);
            }
        }

        public Dictionary<string, FsEntry> Run(string path, ExcludeList excludeList)
        {
            _excludeList = excludeList;
            ScanPath(path, "");
            return _fileList;
        }
    }
}
