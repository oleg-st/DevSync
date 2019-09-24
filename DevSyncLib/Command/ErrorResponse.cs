namespace DevSyncLib.Command
{
    public class ErrorResponse : Packet
    {
        public override int Signature => 0;

        public bool Recoverable;
        public string Message;

        public override void Read(Reader reader)
        {
            Message = reader.ReadString();
            Recoverable = reader.ReadBool();
        }

        public override void Write(Writer writer)
        {
            writer.WriteString(Message);
            writer.WriteBool(Recoverable);
        }
    }
}
