using DevSyncLib.Logger;

namespace DevSyncLib.Command
{
    public abstract class Packet
    {
        protected readonly ILogger _logger;

        protected Packet(ILogger logger)
        {
            _logger = logger;
        }

        public abstract short Signature { get; }

        public abstract void Read(Reader reader);

        public abstract void Write(Writer writer);
    }
}
