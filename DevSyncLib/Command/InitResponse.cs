using DevSyncLib.Logger;

namespace DevSyncLib.Command
{
    public class InitResponse : Packet
    {
        public override short Signature => 2;

        public bool Ok;

        public override void Read(Reader reader)
        {
            Ok = reader.ReadBool();
        }

        public override void Write(Writer writer)
        {
            writer.WriteBool(Ok);
        }

        public InitResponse(ILogger logger) : base(logger)
        {
        }
    }
}
