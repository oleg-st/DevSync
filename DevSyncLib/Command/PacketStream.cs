using System.Collections.Generic;
using System.IO;
using DevSyncLib.Command.Compression;

namespace DevSyncLib.Command
{
    public class PacketStream
    {
        protected Reader Reader;
        protected Writer Writer;
        protected Dictionary<int, Packet> Packets;
        protected ICompress Compress;

        public PacketStream(Stream inputStream, Stream outputStream)
        {
            Compress = new BrotliCompression();

            inputStream = new ChunkReadStream(inputStream, Compress);
            outputStream = new ChunkWriteStream(outputStream, Compress);

            Reader = new Reader(inputStream);
            Writer = new Writer(outputStream);

            Packets = new Dictionary<int, Packet>();
            RegisterPacket(new ErrorResponse());
            RegisterPacket(new InitRequest());
            RegisterPacket(new InitResponse());
            RegisterPacket(new ScanRequest());
            RegisterPacket(new ScanResponse());
            RegisterPacket(new ApplyRequest());
            RegisterPacket(new ApplyResponse());
        }

        protected void RegisterPacket(Packet packet)
        {
            Packets.Add(packet.Signature, packet);
        }

        public Packet ReadPacket()
        {
            int packetSignature = Reader.ReadInt();
            if (!Packets.TryGetValue(packetSignature, out var command))
            {
                throw new SyncException($"Unknown packet: {packetSignature}");
            }
            command.Read(Reader);

            if (command is ErrorResponse errorResponse)
            {
                throw new SyncException("Agent responded error: " + errorResponse.Message, errorResponse.Recoverable);
            }

            return command;
        }

        public void WritePacket(Packet packet)
        {
            int commandSignature = packet.Signature;
            if (!Packets.ContainsKey(commandSignature))
            {
                throw new SyncException($"Unknown packet: {commandSignature}");
            }

            Writer.WriteInt(commandSignature);
            packet.Write(Writer);
            Writer.Flush();
        }

        public T SendCommand<T>(Packet packet) where T: class
        {
            WritePacket(packet);
            var responsePacket = ReadPacket();
            var response = responsePacket as T;
            if (response != null)
            {
                return response;
            }

            throw new SyncException($"Invalid response: {packet.GetType()}");
        }
    }
}
