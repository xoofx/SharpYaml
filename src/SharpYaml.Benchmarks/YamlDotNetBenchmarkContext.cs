using YamlDotNet.Serialization;

namespace SharpYaml.Benchmarks;

[YamlStaticContext]
[YamlSerializable(typeof(BenchmarkDocument))]
public partial class YamlDotNetBenchmarkContext : StaticContext
{
}
