using DevSyncLib.Command;
using DevSyncLib.Logger;
using System;
using System.IO;
using System.Reflection;

namespace DevSyncAgent
{
    class Program
    {
        static void Main(string[] args)
        {
            var assemblyPath = Path.GetDirectoryName(Assembly.GetCallingAssembly().Location);
            var logger = new FileLogger(Path.Combine(assemblyPath, "devsync.log"));
            try
            {
                if (!Console.IsInputRedirected || !Console.IsOutputRedirected)
                {
                    Console.WriteLine("DevSyncAgent: No sender");
                    return;
                }

                PosixExtensions.SetupUserMask();

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
