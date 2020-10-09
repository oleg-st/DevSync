﻿using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Running;

namespace DevSyncBenchmark
{
    class Program
    {
        static void Main(string[] args)
        {
            var config = ManualConfig
                .Create(DefaultConfig.Instance)
                .WithOptions(ConfigOptions.DisableLogFile)
                .WithOptions(ConfigOptions.JoinSummary);

            BenchmarkRunner.Run(typeof(Program).Assembly, config);
        }
    }
}
