using DevSyncLib.Logger;

namespace DevSyncLib.Command;

public class ScanRequest(ILogger logger) : Packet(logger)
{
    public override short Signature => 3;

    public override void Read(Reader reader)
    {
    }

    public override void Write(Writer writer)
    {
    }
}