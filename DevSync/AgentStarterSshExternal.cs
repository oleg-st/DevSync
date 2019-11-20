using System.Diagnostics;
using System.Linq;
using DevSyncLib;
using DevSyncLib.Command;
using DevSyncLib.Logger;
using Medallion.Shell;

namespace DevSync
{
    public class AgentStarterSshExternal: AgentStarter
    {
        private Command _command;

        private string _host, _username, _keyFilePath;

        public string Host
        {
            get => _host;
            set
            {
                _host = value;
                IsStarted = false;
            }
        }

        public string Username
        {
            get => _username;
            set
            {
                _username = value;
                IsStarted = false;
            }
        }

        public string KeyFilePath
        {
            get => _keyFilePath;
            set
            {
                _keyFilePath = value;
                IsStarted = false;
            }
        }

        public bool DeployAgent { get; set; }

        protected override void Cleanup()
        {
            _command?.Kill();
        }

        protected void DoDeployAgent(string path)
        {
            var sw = Stopwatch.StartNew();
            var tarBytes = CreateTarForAgent();
            using var sshCommand = RunSsh(
                "mkdir",
                "-p",
                path,
                "&&",
                "tar",
                "xf",
                "-",
                "-C",
                path
            );
            using (sshCommand.StandardInput)
            {
                sshCommand.StandardInput.BaseStream.Write(tarBytes);
            }
            sshCommand.Task.Wait();

            if (sshCommand.Task.Result.ExitCode != 0)
            {
                throw new SyncException(
                    $"Agent deploy failed {path} ({sshCommand.Task.Result.ExitCode}, {sshCommand.Result.StandardError})");
            }

            Logger.Log($"Agent deployed in {sw.ElapsedMilliseconds} ms");
        }

        protected string GetSshExecutable()
        {
            // TODO: add command line option
            return "ssh";
        }

        protected object[] GetSshOptions(params string[] additionalOptions)
        {
            return new object[]
            {
                // no pseudo terminal
                "-T",
                // remove interaction
                "-o",
                "BatchMode yes",
                // turn off host key checking
                "-o",
                "StrictHostKeyChecking no",
                // disable escape char (transparent binary traffic)
                "-o",
                "EscapeChar none",
                // keep/check server alive
                "-o",
                "ServerAliveInterval 30",
                // specify key file path
                "-i",
                _keyFilePath,
                // quiet mode
                "-q",
                "-l",
                _username,
                _host
            }.Concat(additionalOptions).ToArray();
        }

        private Command RunSsh(params string[] args)
        {
            var options = GetSshOptions(args);
            return Command.Run(GetSshExecutable(), options);
        }

        public override void DoStart()
        {
            if (DeployAgent)
            {
                DoDeployAgent(DeployPath);
            }

            // TODO: private key pass phrase is not supported (no interaction with user)
            _command = RunSsh(
                "COMPlus_EnableDiagnostics=0",
                "dotnet",
                $"{DeployPath}/DevSyncAgent.dll"
            );
            PacketStream = new PacketStream(_command.StandardOutput.BaseStream, _command.StandardInput.BaseStream, Logger);
            _command.Task.ContinueWith(r =>
            {
                SetAgentExitCode(r.Result.ExitCode, r.Result.StandardError);
                Cleanup();
                IsStarted = false;
            });
        }

        public AgentStarterSshExternal(ILogger logger) : base(logger)
        {
        }
    }
}
