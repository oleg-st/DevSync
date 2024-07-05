using DevSyncLib.Logger;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;

namespace DevSyncLib;

public class ScanDirectory(
    ILogger logger,
    FileMaskList excludeList,
    bool withInfo = true,
    CancellationToken? cancellationToken = null)
{
    public IEnumerable<FsEntry> ScanPath(string basePath, string relativePath = "") => 
        ScanPath(new DirectoryInfo(Path.Combine(basePath, relativePath)), relativePath);

    public IEnumerable<FsEntry> ScanPath(DirectoryInfo directoryInfo, string relativePath = "")
    {
        IEnumerable<FileSystemInfo>? fileSystemInfos = null;
        try
        {
            if (directoryInfo.Exists)
            {
                fileSystemInfos = directoryInfo.EnumerateFileSystemInfos("*", new EnumerationOptions
                {
                    ReturnSpecialDirectories = false,
                    // Skip symlinks
                    AttributesToSkip = FileAttributes.ReparsePoint
                });
            }
        }
        catch (DirectoryNotFoundException)
        {
            // directory vanished during scan
        }
        catch (Exception ex)
        {
            logger.Log($"Error scanning directory: {ex.Message}", LogLevel.Warning);
        }

        if (fileSystemInfos != null)
        {
            foreach (var fsInfo in fileSystemInfos)
            {
                cancellationToken?.ThrowIfCancellationRequested();
                // U+FFFD is the "Unicode replacement character"
                // Skip names with text encoding problems, we can't handle them
                if (fsInfo.Name.Contains((char)0xFFFD))
                {
                    continue;
                }

                var path = string.IsNullOrEmpty(relativePath) ? fsInfo.Name : $"{relativePath}/{fsInfo.Name}";
                var fsEntry = FsEntry.Empty;

                // skip excludes
                if (!excludeList.IsMatch(path))
                {
                    // scan children
                    if (fsInfo is DirectoryInfo childDirectoryInfo)
                    {
                        foreach (var entry in ScanPath(childDirectoryInfo, path))
                        {
                            yield return entry;
                        }
                    }

                    try
                    {
                        fsEntry = FsEntry.FromFsInfo(path, fsInfo, withInfo);
                    }
                    catch (DirectoryNotFoundException)
                    {
                        // directory vanished during scan
                    }
                    catch (Exception ex)
                    {
                        logger.Log($"Error scanning directory: {ex.Message}", LogLevel.Warning);
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