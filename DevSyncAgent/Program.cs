using System;
using System.IO;
using DevSyncLib;
using DevSyncLib.Command;

namespace DevSyncAgent
{
    class Program
    {
        static void Main(string[] args)
        {
            try
            {
                if (!Console.IsInputRedirected || !Console.IsOutputRedirected)
                {
                    Console.WriteLine("DevSyncAgent: No sender");
                    return;
                }

                var packetStream = new PacketStream(Console.OpenStandardInput(), Console.OpenStandardOutput());
                var commandRunner = new CommandRunner();
                while (true)
                {
                    var request = packetStream.ReadPacket();
                    var response = commandRunner.Run(request);
                    packetStream.WritePacket(response);
                }
            }
            catch (EndOfStreamException)
            {
                // end of input data
            }
            catch (Exception e)
            {
                Console.Error.WriteLine(e);
                Logger.Log(e.ToString());
            }
        }
    }
}
