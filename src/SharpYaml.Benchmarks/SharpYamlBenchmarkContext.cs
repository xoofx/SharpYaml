using SharpYaml.Serialization;

namespace SharpYaml.Benchmarks;

[YamlSerializable(typeof(BenchmarkDocument))]
[YamlSerializable(typeof(DatabaseConfiguration))]
[YamlSerializable(typeof(ServiceConfiguration))]
[YamlSerializable(typeof(List<ServiceConfiguration>))]
[YamlSerializable(typeof(List<string>))]
[YamlSerializable(typeof(Dictionary<string, string>))]
internal partial class SharpYamlBenchmarkContext : YamlSerializerContext
{
}
