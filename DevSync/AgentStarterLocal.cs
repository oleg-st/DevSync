using DevSyncLib.Command;
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
            _command = Command.Run("dotnet", "DevSyncAgent.dll");
            PacketStream = new PacketStream(_command.StandardOutput.BaseStream, _command.StandardInput.BaseStream);
        }
    }
}
