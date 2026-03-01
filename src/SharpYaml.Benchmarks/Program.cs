using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Running;

var config = DefaultConfig.Instance.WithOptions(ConfigOptions.JoinSummary);

BenchmarkSwitcher
    .FromAssembly(typeof(Program).Assembly)
    .Run(args, config);
