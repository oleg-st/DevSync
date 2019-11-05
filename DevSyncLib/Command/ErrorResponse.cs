using DevSyncLib.Logger;

namespace DevSyncLib.Command
{
    public class ErrorResponse : Packet
    {
        public override short Signature => 0;

        public bool Recoverable, NeedToWait;
        public string Message;

        public override void Read(Reader reader)
        {
            Message = reader.ReadString();
            Recoverable = reader.ReadBool();
            NeedToWait = reader.ReadBool();
        }

        public override void Write(Writer writer)
        {
            writer.WriteString(Message);
            writer.WriteBool(Recoverable);
            writer.WriteBool(NeedToWait);
        }

        public ErrorResponse(ILogger logger) : base(logger)
        {
        }
    }
}
