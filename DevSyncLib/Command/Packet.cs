using DevSyncLib.Logger;

namespace DevSyncLib.Command;

public abstract class Packet(ILogger logger)
{
    protected readonly ILogger Logger = logger;

    public abstract short Signature { get; }

    public abstract void Read(Reader reader);

    public abstract void Write(Writer writer);
}