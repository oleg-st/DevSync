using DevSyncLib;
using DevSyncLib.Logger;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Text.RegularExpressions;

namespace DevSync;

public class SyncOptions
{
    public const int DefaultPort = 22;
    public string? SourcePath { get; set; }
    public string? Host { get; set; }
    public int Port { get; set; } = DefaultPort;
    public string? UserName { get; set; }
    public string? DestinationPath { get; set; }
    public List<string> ExcludeList { get; set; } = [];

    public bool DeployAgent { get; set; }

    public string KeyFilePath { get; set; } = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".ssh/id_rsa");

    public bool ExternalSsh { get; set; }

    public bool AuthorizeKey { get; set; }

    public static SyncOptions CreateFromSourceAndDestination(string sourcePath, string destinationPath, int port)
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
            Port = port,
            UserName = syncPath.UserName,
            DestinationPath = syncPath.Path,
            SourcePath = sourcePath
        };
    }

    public static SyncOptions CreateFromRealsyncDirectory(string path, ILogger logger) => 
        CreateFromRealsyncFile(Path.Combine(path, ".realsync"), logger);

    public static SyncOptions CreateFromRealsyncFile(string filename, ILogger logger)
    {
        if (!File.Exists(filename))
        {
            throw new SyncException($"File is not exists {filename}");
        }

        var syncOptions = new SyncOptions();

        // local = D:/Work/Project
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
                    // local path may be relative to directory of filename
                    syncOptions.SourcePath = Path.GetFullPath(value, Path.GetFullPath(Path.GetDirectoryName(filename) ?? Environment.CurrentDirectory));
                    break;
                case "remote":
                    syncOptions.DestinationPath = value;
                    break;
                case "user":
                    syncOptions.UserName = value;
                    break;
                case "host":
                    if (Uri.TryCreate($"tcp://{value}", UriKind.Absolute, out var uri))
                    {
                        syncOptions.Host = uri.Host;
                        syncOptions.Port = uri.Port < 0 ? DefaultPort : uri.Port;
                    }
                    else
                    {
                        syncOptions.Host = value;
                    }
                    break;
                case "exclude":
                    syncOptions.ExcludeList.Add(value);
                    break;
                case "nosound":
                    break;
                default:
                    logger.Log($"Unknown realsync option: {key}");
                    break;
            }
        }

        if (!syncOptions.IsFilled)
        {
            throw new SyncException("Not enough options from realsync file");
        }

        return syncOptions;
    }

    [MemberNotNullWhen(true, nameof(SourcePath))]
    [MemberNotNullWhen(true, nameof(DestinationPath))]
    [MemberNotNullWhen(true, nameof(UserName))]
    [MemberNotNullWhen(true, nameof(Host))]
    public bool IsFilled => !string.IsNullOrWhiteSpace(SourcePath) &&
                            !string.IsNullOrWhiteSpace(DestinationPath) &&
                            UserName != null &&
                            Host != null;

    public override string ToString() => 
        $"{SourcePath} -> {UserName}@{Host}:{DestinationPath}, {ExcludeList.Count} excludes{(Port != DefaultPort ? $", port {Port}" : "")}{(DeployAgent ? ", deploy" : "")}{(ExternalSsh ? ", external ssh" : "")}{(AuthorizeKey ? ", authorize key" : "")}";
}