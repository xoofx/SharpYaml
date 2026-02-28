using BenchmarkDotNet.Attributes;
using YamlDotNet.Serialization;

namespace SharpYaml.Benchmarks;

[MemoryDiagnoser]
public class GenericSerializationBenchmarks
{
    private Dictionary<string, object?> _genericDocument = null!;
    private List<string> _stringList = null!;
    private ISerializer _yamlDotNetSerializer = null!;

    [GlobalSetup]
    public void Setup()
    {
        _genericDocument = BenchmarkDataFactory.CreateGenericDocument(serviceCount: 200, endpointCountPerService: 12);
        _stringList = BenchmarkDataFactory.CreateStringList(count: 8000);
        _yamlDotNetSerializer = new SerializerBuilder().Build();
    }

    [Benchmark(Baseline = true)]
    public string SharpYaml_Serialize_GenericDictionary()
    {
        return SharpYaml.YamlSerializer.Serialize(_genericDocument);
    }

    [Benchmark]
    public string YamlDotNet_Serialize_GenericDictionary()
    {
        return _yamlDotNetSerializer.Serialize(_genericDocument);
    }

    [Benchmark]
    public string SharpYaml_Serialize_StringList()
    {
        return SharpYaml.YamlSerializer.Serialize(_stringList);
    }

    [Benchmark]
    public string YamlDotNet_Serialize_StringList()
    {
        return _yamlDotNetSerializer.Serialize(_stringList);
    }
}
