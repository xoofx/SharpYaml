using System.Text.Json.Serialization;
using SharpYaml.Serialization;

namespace SharpYaml.Benchmarks;

[JsonSerializable(typeof(BenchmarkDocument))]
[JsonSerializable(typeof(DatabaseConfiguration))]
[JsonSerializable(typeof(ServiceConfiguration))]
[JsonSerializable(typeof(List<ServiceConfiguration>))]
[JsonSerializable(typeof(List<string>))]
[JsonSerializable(typeof(Dictionary<string, string>))]
internal partial class SharpYamlBenchmarkContext : YamlSerializerContext
{
}
