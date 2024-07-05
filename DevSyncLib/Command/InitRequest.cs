using DevSyncLib.Logger;
using System.Collections.Generic;
using System.Diagnostics;

namespace DevSyncLib.Command;

public class InitRequest(ILogger logger) : Packet(logger)
{
    public override short Signature => 1;
    public AgentOptions? AgentOptions;

    public override void Read(Reader reader)
    {
        AgentOptions = new AgentOptions(reader.ReadString(), []);
        var masksCount = reader.ReadInt();
        for (var i = 0; i < masksCount; i++)
        {
            AgentOptions.ExcludeList.Add(reader.ReadString());
        }
    }

    public override void Write(Writer writer)
    {
        Debug.Assert(AgentOptions != null);
        writer.WriteString(AgentOptions.DestPath);
        writer.WriteInt(AgentOptions.ExcludeList.Count);
        foreach (var mask in AgentOptions.ExcludeList)
        {
            writer.WriteString(mask);
        }
    }
}