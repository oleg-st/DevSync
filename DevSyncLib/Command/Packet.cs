namespace DevSyncLib.Command
{
    public abstract class Packet
    {
        public abstract int Signature { get; }

        public abstract void Read(Reader reader);

        public abstract void Write(Writer writer);
    }
}
