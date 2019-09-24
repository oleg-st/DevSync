using System;
using System.Collections.Generic;
using System.IO;

namespace DevSyncLib
{
    public class ScanDirectory
    {
        public Dictionary<string, FsEntry> FileList { get; private set; }
        private ExcludeList _excludeList;

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
                            FileList.Add(path, FsEntry.FromFileInfo(path, file));
                        }
                        catch (Exception ex)
                        {
                            // TODO: ignored errors
                            Logger.Log($"Scan error {ex}");
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
                            FileList.Add(path, FsEntry.FromDirectoryInfo(path, dir));
                        }
                        catch (Exception ex)
                        {
                            // TODO: ignored errors
                            Logger.Log($"Scan error {ex}");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                // TODO: ignored errors
                Logger.Log($"Scan error {ex}");
            }
        }

        public void Run(string path, ExcludeList excludeList)
        {
            _excludeList = excludeList;
            FileList = new Dictionary<string, FsEntry>();
            ScanPath(path, "");
        }
    }
}
