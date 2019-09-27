using System;
using System.Collections.Generic;
using System.ComponentModel.Design;
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

            [Option("deploy", Default = false, HelpText = "Deploy agent")]
            public bool DeployAgent { get; set; }

            [Option("realsync", Default = null, HelpText = "Realsync source directory")]
            public string RealsyncPath { get; set; }
        }

        static void PrintHelp(ParserResult<CommandLineOptions> parserResult)
        {
            var optionsUsage = HelpText.AutoBuild(parserResult, text => text, example => example);
            optionsUsage.Heading = "DevSync";
            optionsUsage.Copyright = "by Oleg Stepanischev";
            optionsUsage.AdditionalNewLineAfterOption = false;
            optionsUsage.AddDashesToOption = true;
            optionsUsage.AddPreOptionsLine($"Usage: dotnet DevSync <in> <out> [options]");
            optionsUsage.AddPreOptionsLine("Options:");
            optionsUsage.AddOptions(parserResult);
            Console.Error.WriteLine(optionsUsage);
            Environment.Exit(-1);
        }

        static void Main(string[] args)
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
                    SyncOptions syncOptions = null;

                    if (!string.IsNullOrEmpty(options.RealsyncPath))
                    {
                        syncOptions = SyncOptions.CreateFromRealsyncDirectory(options.RealsyncPath);

                    }
                    else if (!string.IsNullOrEmpty(options.SourcePath) && !string.IsNullOrEmpty(options.DestinationPath))
                    {
                        syncOptions =
                            SyncOptions.CreateFromSourceAndDestination(options.SourcePath, options.DestinationPath);
                        // TODO: remove hardcoded exclude list
                        syncOptions.ExcludeList.AddRange(new []
                        {
                            "~*", "*.tmp", "*.pyc", "*.swp", ".git", "CVS", ".svn", ".realsync", ".cache", ".idea", "nbproject",
                            "config_cache.inc",
                            "admin/tools/sql", "include/9", "tmp", "tmp-project", "node_modules", "js/vue/build", "storage",
                            "var/log", "var/medo", "courses"
                        });
                    }

                    if (syncOptions == null)
                    {
                        PrintHelp(parserResult);
                    }

                    using var sender = new Sender(syncOptions, options.DeployAgent, logger);
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
