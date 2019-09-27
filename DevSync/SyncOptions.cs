using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using DevSyncLib;

namespace DevSync
{
    public class SyncOptions
    {
        public string SourcePath { get; set; }
        public string Host { get; set; }
        public string UserName { get; set; }
        public string DestinationPath { get; set; }
        public List<string> ExcludeList { get; set; } = new List<string>();

        public bool DeployAgent { get; set; }

        public static SyncOptions CreateFromSourceAndDestination(string sourcePath, string destinationPath)
        {
            if (string.IsNullOrWhiteSpace(sourcePath))
            {
                throw new SyncException($"Invalid source path: {sourcePath}");
            }

            var syncPath = SyncPath.Parse(destinationPath);
            if (syncPath == null)
            {
                throw new SyncException($"Invalid destination path: {destinationPath}");
            }

            return new SyncOptions
            {
                Host = syncPath.Host,
                UserName = syncPath.UserName,
                DestinationPath = syncPath.Path,
                SourcePath = sourcePath
            };
        }

        public static SyncOptions CreateFromRealsyncDirectory(string path)
        {
            return CreateFromRealsyncFile(Path.Combine(path, ".realsync"));
        }

        public static SyncOptions CreateFromRealsyncFile(string filename)
        {
            if (!File.Exists(filename))
            {
                throw new SyncException($"File is not exists {filename}");
            }

            var syncOptions = new SyncOptions();

            // local = D:/Work/SEDv1-MSK-3
            var optionRegex = new Regex("^(\\S+)\\s*=\\s*(.*)$", RegexOptions.Compiled);

            var lines = File.ReadAllLines(filename);
            foreach (var line in lines)
            {
                var trimmedLine = line.TrimStart();
                // skip comment

                if (trimmedLine.StartsWith("#") || string.IsNullOrWhiteSpace(trimmedLine))
                {
                    continue;
                }

                var match = optionRegex.Match(trimmedLine);
                if (!match.Success)
                {
                    throw new SyncException($"Invalid line: {line}");
                }

                var key = match.Groups[1].Value.ToLower();
                var value = match.Groups[2].Value;

                switch (key)
                {
                    case "local":
                        syncOptions.SourcePath = value;
                        break;
                    case "remote":
                        syncOptions.DestinationPath = value;
                        break;
                    case "user":
                        syncOptions.UserName = value;
                        break;
                    case "host":
                        syncOptions.Host = value;
                        break;
                    case "exclude":
                        syncOptions.ExcludeList.Add(value);
                        break;
                    case "nosound":
                        break;
                    default:
                        throw new SyncException($"Unknown realsync option: {key}");
                }
            }

            if (!syncOptions.IsFilled)
            {
                throw new SyncException("Not enough options from realsync file");
            }

            return syncOptions;
        }

        public bool IsFilled => !string.IsNullOrWhiteSpace(SourcePath) &&
                                !string.IsNullOrWhiteSpace(DestinationPath) &&
                                !string.IsNullOrWhiteSpace(UserName) &&
                                !string.IsNullOrWhiteSpace(Host);

        public override string ToString()
        {
            return $"{SourcePath} -> {UserName}@{Host}:{DestinationPath}, {ExcludeList.Count} excludes{(DeployAgent ? ", deploy" : "")}";
        }
    }
}
