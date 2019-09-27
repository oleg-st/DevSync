using System;
using System.IO;
using DevSyncLib.Command;
using DevSyncLib.Logger;

namespace DevSyncAgent
{
    class Program
    {
        static void Main(string[] args)
        {
            var logger = new FileLogger( Path.Combine(Path.GetTempPath(), "devsync.log"));
            try
            {
                if (!Console.IsInputRedirected || !Console.IsOutputRedirected)
                {
                    Console.WriteLine("DevSyncAgent: No sender");
                    return;
                }

                var packetStream = new PacketStream(Console.OpenStandardInput(), Console.OpenStandardOutput(), logger);
                var commandRunner = new CommandRunner(logger);
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
                logger.Log(e.ToString(), LogLevel.Error);
            }
        }
    }
}
