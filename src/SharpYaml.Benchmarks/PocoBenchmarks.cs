using BenchmarkDotNet.Attributes;
using YamlDotNet.Serialization;

namespace SharpYaml.Benchmarks;

[MemoryDiagnoser]
public class PocoBenchmarks
{
    private BenchmarkDocument _document = null!;
    private string _documentYaml = string.Empty;
    private ISerializer _yamlDotNetSerializer = null!;
    private IDeserializer _yamlDotNetDeserializer = null!;

    [GlobalSetup]
    public void Setup()
    {
        _document = BenchmarkDataFactory.CreateDocument(serviceCount: 200, endpointCountPerService: 12);
        _yamlDotNetSerializer = new SerializerBuilder().Build();
        _yamlDotNetDeserializer = new DeserializerBuilder().Build();
        _documentYaml = _yamlDotNetSerializer.Serialize(_document);
    }

    [Benchmark(Baseline = true)]
    public string SharpYaml_Serialize_Poco()
    {
        return SharpYaml.YamlSerializer.Serialize(_document);
    }

    [Benchmark]
    public string YamlDotNet_Serialize_Poco()
    {
        return _yamlDotNetSerializer.Serialize(_document);
    }

    [Benchmark]
    public BenchmarkDocument SharpYaml_Deserialize_Poco()
    {
        return SharpYaml.YamlSerializer.Deserialize<BenchmarkDocument>(_documentYaml)!;
    }

    [Benchmark]
    public BenchmarkDocument YamlDotNet_Deserialize_Poco()
    {
        return _yamlDotNetDeserializer.Deserialize<BenchmarkDocument>(_documentYaml)!;
    }
}
