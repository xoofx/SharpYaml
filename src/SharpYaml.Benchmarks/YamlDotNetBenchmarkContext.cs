using YamlDotNet.Serialization;

namespace SharpYaml.Benchmarks;

[YamlStaticContext]
[YamlSerializable(typeof(BenchmarkDocument))]
[YamlSerializable(typeof(DatabaseConfiguration))]
[YamlSerializable(typeof(ServiceConfiguration))]
public partial class YamlDotNetBenchmarkContext : StaticContext
{
}
