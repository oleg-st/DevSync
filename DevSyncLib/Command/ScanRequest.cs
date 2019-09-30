using DevSyncLib.Logger;

namespace DevSyncLib.Command
{
    public class ScanRequest : Packet
    {
        public override short Signature => 3;

        public override void Read(Reader reader)
        {
        }

        public override void Write(Writer writer)
        {
        }

        public ScanRequest(ILogger logger) : base(logger)
        {
        }
    }
}
