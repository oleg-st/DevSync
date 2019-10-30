using System.IO;
using DevSyncLib.Command;
using DevSyncLib.Logger;
using Medallion.Shell;

namespace DevSync
{
    public class AgentStarterLocal: AgentStarter
    {
        private Command _command;

        protected override void Cleanup()
        {
            _command?.Kill();
        }

        public override void DoStart()
        {
            var agentPath = Path.Combine(Path.GetDirectoryName(typeof(PacketStream).Assembly.Location), "DevSyncAgent.dll");
            _command = Command.Run("dotnet", agentPath);
            PacketStream = new PacketStream(_command.StandardOutput.BaseStream, _command.StandardInput.BaseStream, Logger);
        }

        public AgentStarterLocal(ILogger logger) : base(logger)
        {
        }
    }
}
