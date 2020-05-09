using System.Diagnostics;
using System.IO;
using DevSyncLib.Command;
using DevSyncLib.Logger;

namespace DevSync
{
    public class AgentStarterLocal: AgentStarter
    {
        private Process _process;

        protected override void Cleanup()
        {
            try
            {
                // the try-catch is because Kill() will throw if the process is disposed
                _process?.Kill();
            }
            catch
            {
                // ignore errors
            }
        }

        public override void DoStart()
        {
            var agentPath = Path.Combine(Path.GetDirectoryName(typeof(PacketStream).Assembly.Location), "DevSyncAgent.dll");
            var processStartInfo = new ProcessStartInfo
            {
                FileName = "dotnet",
                RedirectStandardError = true,
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                UseShellExecute = false,
            };
            /*
             * COMPlus_EnableDiagnostics turns off clr-debug-pipe
             * https://github.com/dotnet/coreclr/blob/master/Documentation/building/debugging-instructions.md
             */
            processStartInfo.Environment["COMPlus_EnableDiagnostics"] = "0";
            processStartInfo.ArgumentList.Add(agentPath);
            _process = new Process { StartInfo = processStartInfo, EnableRaisingEvents = true };
            _process.Exited += (sender, args) =>
            {
                SetAgentExitCode(_process.ExitCode, _process.StandardError.ReadToEnd());
                Cleanup();
                IsStarted = false;
            };
            _process.Start();
            PacketStream = new PacketStream(_process.StandardOutput.BaseStream, _process.StandardInput.BaseStream, Logger);
        }

        public AgentStarterLocal(ILogger logger) : base(logger)
        {
        }
    }
}
