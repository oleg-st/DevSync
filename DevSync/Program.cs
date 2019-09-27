using System;
using System.Collections.Generic;
using CommandLine;
using CommandLine.Text;

namespace DevSync
{
    class Program
    {
        public class CommandLineOptions
        {
            [Value(0, Required = true, HelpText = "Source path", MetaName = "source")]
            public string SourcePath { get; set; }

            [Value(1, Required = true, HelpText = "Destination path", MetaName = "destination")]
            public string DestinationPath { get; set; }

            [Option("deploy", Default = false, HelpText = "Deploy agent")]
            public bool DeployAgent { get; set; }
        }

        static void Main(string[] args)
        {
            var parser = new Parser(with =>
            {
                with.AutoHelp = false;
            });

            var parserResult = parser.ParseArguments<CommandLineOptions>(args);
            parserResult.WithNotParsed(errors =>
            {
                var optionsUsage = HelpText.AutoBuild(parserResult);
                optionsUsage.Heading = "DevSync";
                optionsUsage.Copyright = "by Oleg Stepanischev";
                optionsUsage.AdditionalNewLineAfterOption = false;
                optionsUsage.AddDashesToOption = true;
                optionsUsage.AddPreOptionsLine($"Usage: dotnet DevSync <in> <out> [options]");
                optionsUsage.AddPreOptionsLine("Options:");
                optionsUsage.AddOptions(parserResult);
                Console.Error.WriteLine(optionsUsage);
                Environment.Exit(-1);
            });

            parserResult.WithParsed(options =>
            {
                var excludes = new List<string>
                {
                    "~*", "*.tmp", "*.pyc", "*.swp", ".git", "CVS", ".svn", ".realsync", ".cache", ".idea", "nbproject",
                    "config_cache.inc",
                    "admin/tools/sql", "include/9", "tmp", "tmp-project", "node_modules", "js/vue/build", "storage",
                    "var/log", "var/medo", "courses"
                };

                try
                {
                    using var sender = new Sender(options.SourcePath, options.DestinationPath, excludes, options.DeployAgent);
                    sender.Run();
                }
                catch (Exception ex)
                {
                    Console.Error.WriteLine(ex.Message);
                    Environment.Exit(-1);
                }
            });
        }
    }
}
