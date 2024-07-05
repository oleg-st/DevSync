using DevSync.SshStarter;
using DevSyncLib;
using DevSyncLib.Command;
using DevSyncLib.Logger;
using ICSharpCode.SharpZipLib.Tar;
using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading.Tasks;

namespace DevSync;

public class AgentStarterSsh(ILogger logger) : AgentStarter(logger)
{
    private ISshStarter? _sshStarter;
    private ISshStarterCommand? _sshStarterCommand;

    private string? _host, _username, _keyFilePath;
    private int _port;

    private AuthenticationMethodMode _authenticationMethodMode;

    public string? Host
    {
        get => _host;
        set
        {
            _host = value;
            IsStarted = false;
        }
    }

    public int Port
    {
        get => _port;
        set
        {
            _port = value;
            IsStarted = false;
        }
    }

    public string? Username
    {
        get => _username;
        set
        {
            _username = value;
            IsStarted = false;
        }
    }

    public string? KeyFilePath
    {
        get => _keyFilePath;
        set
        {
            _keyFilePath = value;
            IsStarted = false;
        }
    }

    public bool DeployAgent { get; set; }

    public bool AuthorizeKey { get; set; }

    public bool ExternalSsh { get; set; }

    /*
     * Occurs when shell cannot find dotnet command
     *
     * Command not found:
     * http://www.tldp.org/LDP/abs/html/exitcodes.html
     */
    protected const byte CommandNotFoundCode = 127;

    /*
     * Occurs when DevSync agent is not found and .NET Core SDK is installed.
     */
    protected const byte NotFoundDotnet = 1;

    /*
     * Occurs when DevSync agent is not found and .NET Core SDK is not installed.
     *
     * LibHostSdkFindFailure:
     * https://github.com/dotnet/core-setup/blob/master/Documentation/design-docs/host-error-codes.md
     */
    protected const byte LibHostSdkFindFailure = 0x91;

    /*
     * Occurs when DevSync agent dependencies is not found.
     * ResolverResolveFailure:
     * https://github.com/dotnet/core-setup/blob/master/Documentation/design-docs/host-error-codes.md
     */
    protected const byte ResolverResolveFailure = 0x8c;

    protected override void Cleanup()
    {
        base.Cleanup();
        IsStarted = false;
        ISshStarter? sshClient;
        ISshStarterCommand? sshCommand;
        lock (this)
        {
            sshClient = _sshStarter;
            sshCommand = _sshStarterCommand;
        }

        sshCommand?.Dispose();
        sshClient?.Dispose();
    }

    private void CleanupDeferred()
    {
        base.Cleanup();
        IsStarted = false;
        ISshStarter? sshClient;
        ISshStarterCommand? sshCommand;
        lock (this)
        {
            sshClient = _sshStarter;
            sshCommand = _sshStarterCommand;
        }

        Task.Run(() =>
        {
            sshCommand?.Dispose();
            sshClient?.Dispose();
        });
    }

    protected byte[] CreateTarForAgent()
    {
        var files = new[]
        {
            "DevSyncAgent.dll",
            "DevSyncAgent.deps.json",
            "DevSyncAgent.runtimeconfig.json",
            "DevSyncLib.dll",
            "DevSyncLib.deps.json"
        };

        using var memoryStream = new MemoryStream();
        using (var tarArchive = TarArchive.CreateOutputTarArchive(memoryStream))
        {
            var assemblyPath = GetAssemblyDirectoryName();

            foreach (var filename in files)
            {
                var tarEntry = TarEntry.CreateEntryFromFile(Path.Combine(assemblyPath, filename));
                tarEntry.Name = Path.GetFileName(filename);
                tarArchive.WriteEntry(tarEntry, false);
            }
        }

        return memoryStream.ToArray();
    }

    protected void DoDeployAgent(ISshStarter sshStarter, string path)
    {
        var sw = SlimStopwatch.StartNew();
        var tarBytes = CreateTarForAgent();
        using var sshCommand = sshStarter.RunCommand($"mkdir -p {path} && tar xf - -C {path}");
        lock (this)
        {
            _sshStarterCommand = sshCommand;
        }
        try
        {
            using (sshCommand.InputStream)
            {
                sshCommand.InputStream.Write(tarBytes, 0, tarBytes.Length);
            }
        }
        catch (ObjectDisposedException)
        {
        }
        catch (EndOfStreamException)
        {
        }
        catch (IOException)
        {
        }
        sshCommand.Wait();
        lock (this)
        {
            _sshStarterCommand = null;
        }
        if (sshCommand.ExitCode != 0)
        {
            throw new SyncException(
                $"Agent deploy failed {path} ({sshCommand.ExitCode}, {sshCommand.Error.Trim()})");
        }

        Logger.Log($"Agent deployed in {sw.ElapsedMilliseconds} ms");
    }

    protected void DoAuthorizeKey(ISshStarter sshStarter)
    {
        var publicKeyPath = _keyFilePath + ".pub";
        if (!File.Exists(publicKeyPath) || !File.Exists(_keyFilePath))
        {
            var directoryName = Path.GetDirectoryName(_keyFilePath);
            Debug.Assert(directoryName != null);
            if (!Directory.Exists(directoryName))
            {
                Directory.CreateDirectory(directoryName);
                // 0700, rwx------
                PosixExtensions.ChangeMode(directoryName, 0b111_000_000);
            }

            Logger.Log("Generating public/private rsa key pair.");
            var sshKeyGenerator = new SshRsaKeyGenerator();
            Debug.Assert(_keyFilePath!= null);
            using (var fs = new FileStream(_keyFilePath, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                fs.Write(Encoding.ASCII.GetBytes(sshKeyGenerator.ToPrivateKey()));
                // 0600, rw-------
                fs.FChangeMode(0b110_000_000);
            }
            Logger.Log($"Your identification has been saved in {_keyFilePath}");
            File.WriteAllText(publicKeyPath, sshKeyGenerator.ToRfcPublicKey($"{Environment.UserName}@{Environment.MachineName}") + Environment.NewLine);
            Logger.Log($"Your public key has been saved in {publicKeyPath}");
        }
        var publicKey = File.ReadAllText(publicKeyPath).Trim();
        var sw = SlimStopwatch.StartNew();
        using var sshCommand = sshStarter.RunCommand($"mkdir -p .ssh && chmod 700 .ssh && echo {publicKey} >> .ssh/authorized_keys && chmod 600 .ssh/authorized_keys");
        lock (this)
        {
            _sshStarterCommand = sshCommand;
        }
        sshCommand.Wait();
        lock (this)
        {
            _sshStarterCommand = null;
        }
        if (sshCommand.ExitCode != 0)
        {
            throw new SyncException(
                $"Failed to add public key to authorized keys ({sshCommand.ExitCode}, {sshCommand.Error.Trim()})");
        }

        Logger.Log($"Public key was added to authorized keys in {sw.ElapsedMilliseconds} ms");
    }

    private ISshStarter CreateSshStarter()
    {
        Debug.Assert(_host != null);
        Debug.Assert(_keyFilePath != null);
        Debug.Assert(_username != null);
        return ExternalSsh
            ? new SshStarter.External.SshStarter(_host, _port, _keyFilePath, _username, _authenticationMethodMode)
            : new SshStarter.Internal.SshStarter(_host, _port, _keyFilePath, _username, _authenticationMethodMode,
                Logger);
    }

    public override void DoStart()
    {
    restart:
        _authenticationMethodMode = AuthenticationMethodMode.Key;

    retry:
        try
        {
            Cleanup();
            if (_authenticationMethodMode == AuthenticationMethodMode.Key && !File.Exists(KeyFilePath))
            {
                if (!AuthorizeKey)
                {
                    throw new SyncException($"Your ssh private key is not found: {KeyFilePath}. Use ssh-keygen to generate key pair or use --authorize-key option");
                }
                _authenticationMethodMode = AuthenticationMethodMode.Password;
            }
            var sshStarter = CreateSshStarter();
            sshStarter.OnConnectError += (_, args) =>
            {
                Logger.Log(args.Error, LogLevel.Error);
                // use cleanup on other thread to prevent race condition
                CleanupDeferred();
            };
            lock (this)
            {
                _sshStarter = sshStarter;
            }
            sshStarter.Connect();

            if (_authenticationMethodMode == AuthenticationMethodMode.Password && AuthorizeKey)
            {
                DoAuthorizeKey(sshStarter);
                CancellationTokenSource.Token.ThrowIfCancellationRequested();
                AuthorizeKey = false;
                goto restart;
            }

            if (DeployAgent)
            {
                DoDeployAgent(sshStarter, DeployPath);
                CancellationTokenSource.Token.ThrowIfCancellationRequested();
            }

            /*
             * COMPlus_EnableDiagnostics turns off clr-debug-pipe
             * https://github.com/dotnet/coreclr/blob/master/Documentation/building/debugging-instructions.md
             */
            var sshCommand =
                sshStarter.RunCommand($"COMPlus_EnableDiagnostics=0 dotnet {DeployPath}/DevSyncAgent.dll");
            sshCommand.OnExit += (_, _) =>
            {
                SetAgentExitCode(sshCommand.ExitCode ?? -1, sshCommand.Error);
                // use cleanup on other thread to prevent race condition
                CleanupDeferred();
            };
            lock (this)
            {
                _sshStarterCommand = sshCommand;
            }
            PacketStream = new PacketStream(sshCommand.OutputStream, sshCommand.InputStream, Logger);
        }
        catch (SshStarterAuthenticationException ex)
        {
            if (!AuthorizeKey)
            {
                throw new SyncException(ex.Message);
            }
            Logger.Log(ex.Message, LogLevel.Error);
            // fallback to password
            _authenticationMethodMode = AuthenticationMethodMode.Password;
            goto retry;
        }
    }

    protected override void ProcessAgentExitCode()
    {
        // exit code is byte on some platforms
        var byteAgentExitCode = (byte)AgentExitCode;
        if (byteAgentExitCode == CommandNotFoundCode)
        {
            throw new SyncException(".NET Core 3.0 runtime is not installed on destination");
        }

        if (byteAgentExitCode == LibHostSdkFindFailure || byteAgentExitCode == ResolverResolveFailure || byteAgentExitCode == NotFoundDotnet)
        {
            if (DeployAgent)
            {
                throw new SyncException("DevSync agent is not installed on destination");
            }

            DeployAgent = true;
            throw new SyncException("DevSync agent is not installed on destination. Will try to deploy agent", true, false);
        }
    }
}