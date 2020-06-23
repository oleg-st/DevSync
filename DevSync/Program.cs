using System;
using System.IO;
using System.Reflection;
using CommandLine;
using CommandLine.Text;
using DevSyncLib;
using DevSyncLib.Logger;

namespace DevSync
{
    class Program
    {
        public class CommandLineOptions
        {
            [Value(0, HelpText = "Source path", MetaName = "source")]
            public string SourcePath { get; set; }

            [Value(1, HelpText = "Destination path", MetaName = "destination")]
            public string DestinationPath { get; set; }

            [Value(2, HelpText = "Exclude list file", MetaName = "exclude")]
            public string ExcludeListPath { get; set; }

            [Option("no-exclude", Default = false, HelpText = "Do not exclude")]
            public bool NoExclude { get; set; }

            [Option("deploy", Default = true, HelpText = "Deploy agent to ~/.devsync (deprecated)")]
            public bool DeployAgent { get; set; }

            [Option("no-deploy", Default = false, HelpText = "Do not deploy agent")]
            public bool NoDeployAgent { get; set; }

            [Option("external-ssh", Default = false, HelpText = "Use external ssh command")]
            public bool ExternalSsh { get; set; }

            [Option("authorize-key", Default = false, HelpText = "Use password authentication, generate RSA-2048 key pair (if necessary) and setup public key authentication")]
            public bool AuthorizeKey { get; set; }

            [Option("realsync", Default = null, HelpText = "Realsync source directory (with .realsync file)")]
            public string RealsyncPath { get; set; }

            [Option("key", Default = null, HelpText = "Path to the identity (private key) for public key authentication. The default is ~/.ssh/id_dsa")]
            public string KeyFilePath { get; set; }

            [Option("port", Required = false, Default = SyncOptions.DefaultPort, HelpText = "Port to connect to on the remote host.")]
            public int Port { get; set; }
        }

        static void PrintHelp(ParserResult<CommandLineOptions> parserResult)
        {
            var optionsUsage = HelpText.AutoBuild(parserResult, text => text, example => example);
            var version = Assembly.GetCallingAssembly().GetName().Version;
            optionsUsage.Heading = $"DevSync {version.Major}.{version.Minor}";
            optionsUsage.Copyright = "by Oleg Stepanischev";
            optionsUsage.AdditionalNewLineAfterOption = false;
            optionsUsage.AddDashesToOption = true;
            optionsUsage.AddPreOptionsLine("Usage: dotnet DevSync.dll <source> <destination> <exclude file> [options]");
            optionsUsage.AddPreOptionsLine("       dotnet DevSync.dll --realsync <source> [options]");
            optionsUsage.AddPreOptionsLine("Options:");
            optionsUsage.AddOptions(parserResult);
            Console.Error.WriteLine(optionsUsage);
            Environment.Exit(-1);
        }

        private static SyncOptions GetSyncOptions(CommandLineOptions options, ILogger logger)
        {
            SyncOptions syncOptions = null;

            if (!string.IsNullOrEmpty(options.RealsyncPath))
            {
                syncOptions = SyncOptions.CreateFromRealsyncDirectory(options.RealsyncPath, logger);

            }
            else if (!string.IsNullOrEmpty(options.SourcePath) && !string.IsNullOrEmpty(options.DestinationPath))
            {
                syncOptions = SyncOptions.CreateFromSourceAndDestination(options.SourcePath, options.DestinationPath, options.Port);
                if (!options.NoExclude && !string.IsNullOrEmpty(options.ExcludeListPath))
                {
                    syncOptions.ExcludeList.AddRange(File.ReadAllLines(options.ExcludeListPath));
                }
            }

            if (syncOptions == null)
            {
                return null;
            }

            if (!string.IsNullOrEmpty(options.KeyFilePath))
            {
                syncOptions.KeyFilePath = options.KeyFilePath;
            }

            if (!options.NoExclude && syncOptions.ExcludeList.Count == 0)
            {
                throw new SyncException("Empty exclude list, specify --no-exclude if it is intended");
            }

            syncOptions.DeployAgent = !options.NoDeployAgent;
            syncOptions.ExternalSsh = options.ExternalSsh;
            syncOptions.AuthorizeKey = options.AuthorizeKey;
            return syncOptions;
        }

        private static void Main(string[] args)
        {
            var logger = new ConsoleLogger();

            var parser = new Parser(with =>
            {
                with.AutoHelp = false;
            });

            var parserResult = parser.ParseArguments<CommandLineOptions>(args);
            parserResult.WithNotParsed(errors =>
            {
                PrintHelp(parserResult);
            });

            parserResult.WithParsed(options =>
            {
                try
                {
                    var syncOptions = GetSyncOptions(options, logger);
                    if (syncOptions == null)
                    {
                        PrintHelp(parserResult);
                        return;
                    }

                    Console.Title = syncOptions.ToString();
                    using var sender = new Sender(syncOptions, logger);
                    sender.Run();
                }
                catch (Exception ex)
                {
                    logger.Log(ex.Message, LogLevel.Error);
                    Environment.Exit(-1);
                }
            });
        }
    }
}
