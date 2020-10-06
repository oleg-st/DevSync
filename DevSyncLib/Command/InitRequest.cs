using DevSyncLib.Logger;
using System.Collections.Generic;

namespace DevSyncLib.Command
{
    public class InitRequest : Packet
    {
        public override short Signature => 1;
        public AgentOptions AgentOptions;

        public override void Read(Reader reader)
        {
            AgentOptions = new AgentOptions
            {
                DestPath = reader.ReadString()
            };
            var masksCount = reader.ReadInt();
            AgentOptions.ExcludeList = new List<string>();
            for (var i = 0; i < masksCount; i++)
            {
                AgentOptions.ExcludeList.Add(reader.ReadString());
            }
        }

        public override void Write(Writer writer)
        {
            writer.WriteString(AgentOptions.DestPath);
            writer.WriteInt(AgentOptions.ExcludeList.Count);
            foreach (var mask in AgentOptions.ExcludeList)
            {
                writer.WriteString(mask);
            }
        }

        public InitRequest(ILogger logger) : base(logger)
        {
        }
    }
}
