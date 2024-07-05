using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;

namespace DevSync.SshStarter.External;

public class SshStarter(
    string host,
    int port,
    string keyFilePath,
    string username,
    AuthenticationMethodMode authenticationMethodMode)
    : ISshStarter
{
    public void Dispose()
    {
        GC.SuppressFinalize(this);
    }

    public string Host { get; set; } = host;

    public int Port { get; set; } = port;

    public string Username { get; set; } = username;

    public string KeyFilePath { get; set; } = keyFilePath;

    public AuthenticationMethodMode AuthenticationMode { get; set; } = authenticationMethodMode;

    // No connect phase here
    public event EventHandler<SshStarterErrorEventArgs>? OnConnectError;

    public void Connect()
    {
    }

    protected string GetSshExecutable()
    {
        // TODO: add command line option
        return "ssh";
    }

    protected IEnumerable<string> GetSshOptions(params string[] additionalOptions)
    {
        var options = new List<string>
        {
            // no pseudo terminal
            "-T",
            // disable escape char (transparent binary traffic)
            "-o",
            "EscapeChar none",
            // keep/check server alive
            "-o",
            "ServerAliveInterval 30",
            "-l",
            Username,
            "-p",
            Port.ToString(CultureInfo.InvariantCulture)
        };
        if (AuthenticationMode == AuthenticationMethodMode.Key)
        {
            // specify key file path
            options.Add("-i");
            options.Add(KeyFilePath);
            options.Add("-o");
            options.Add("PasswordAuthentication no");
            options.Add("-o");
            options.Add("PubkeyAuthentication yes");
        }
        else
        {
            options.Add("-o");
            options.Add("PasswordAuthentication yes");
            options.Add("-o");
            options.Add("PubkeyAuthentication no");
        }
        options.Add(Host);
        options.AddRange(additionalOptions);
        return options;
    }

    public ISshStarterCommand RunCommand(string command)
    {
        var processStartInfo = new ProcessStartInfo
        {
            FileName = GetSshExecutable(),
            RedirectStandardError = true,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            UseShellExecute = false,
        };
        foreach (var arg in GetSshOptions(command))
        {
            processStartInfo.ArgumentList.Add(arg);
        }
        // Fix home path in ssh
        processStartInfo.Environment["HOME"] = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        return new SshStarterCommand(new Process { StartInfo = processStartInfo, EnableRaisingEvents = true });
    }
}