using DevSyncLib.Logger;

namespace DevSyncLib.Command
{
    public abstract class Packet
    {
        protected readonly ILogger Logger;

        protected Packet(ILogger logger)
        {
            Logger = logger;
        }

        public abstract short Signature { get; }

        public abstract void Read(Reader reader);

        public abstract void Write(Writer writer);
    }
}
