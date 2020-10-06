using DevSyncLib.Command.Compression;
using DevSyncLib.Logger;
using System.Collections.Generic;
using System.IO;

namespace DevSyncLib.Command
{
    public class PacketStream
    {
        protected Reader Reader;
        protected Writer Writer;
        protected Dictionary<short, Packet> Packets;
        protected ICompression Compression;

        public PacketStream(Stream inputStream, Stream outputStream, ILogger logger)
        {
            Compression = new BrotliCompression();

            inputStream = new ChunkReadStream(inputStream, Compression);
            outputStream = new ChunkWriteStream(outputStream, Compression);

            Reader = new Reader(inputStream, logger);
            Writer = new Writer(outputStream, logger);

            Packets = new Dictionary<short, Packet>();
            RegisterPacket(new ErrorResponse(logger));
            RegisterPacket(new InitRequest(logger));
            RegisterPacket(new InitResponse(logger));
            RegisterPacket(new ScanRequest(logger));
            RegisterPacket(new ScanResponse(logger));
            RegisterPacket(new ApplyRequest(logger));
            RegisterPacket(new ApplyResponse(logger));
        }

        protected void RegisterPacket(Packet packet)
        {
            Packets.Add(packet.Signature, packet);
        }

        public Packet ReadPacket()
        {
            short packetSignature = Reader.ReadInt16();
            if (!Packets.TryGetValue(packetSignature, out var command))
            {
                throw new SyncException($"Unknown packet: {packetSignature}");
            }
            command.Read(Reader);

            if (command is ErrorResponse errorResponse)
            {
                throw new SyncException("Agent responded error: " + errorResponse.Message, errorResponse.Recoverable, errorResponse.NeedToWait);
            }

            return command;
        }

        public void WritePacket(Packet packet)
        {
            var commandSignature = packet.Signature;
            if (!Packets.ContainsKey(commandSignature))
            {
                throw new SyncException($"Unknown packet: {commandSignature}");
            }

            Writer.WriteInt16(commandSignature);
            packet.Write(Writer);
            Writer.Flush();
        }

        public T SendCommand<T>(Packet packet) where T : class
        {
            WritePacket(packet);
            var responsePacket = ReadPacket();
            if (responsePacket is T response)
            {
                return response;
            }

            throw new SyncException($"Invalid response: {packet.GetType()}");
        }
    }
}
