using System.Diagnostics;
using DevSyncLib.Logger;

namespace DevSyncLib.Command;

public class ErrorResponse(ILogger logger) : Packet(logger)
{
    public override short Signature => 0;

    public bool Recoverable, NeedToWait;
    public string? Message;

    public override void Read(Reader reader)
    {
        Message = reader.ReadString();
        Recoverable = reader.ReadBool();
        NeedToWait = reader.ReadBool();
    }

    public override void Write(Writer writer)
    {
        Debug.Assert(Message != null);
        writer.WriteString(Message);
        writer.WriteBool(Recoverable);
        writer.WriteBool(NeedToWait);
    }
}