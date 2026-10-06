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
    private string _documentYamlWithUnknownKeys = string.Empty;
    private SharpYaml.YamlSerializerOptions _reportingOptions = null!;
    private int _unmappedCount;

    [GlobalSetup]
    public void Setup()
    {
        _document = BenchmarkDataFactory.CreateDocument(serviceCount: 200, endpointCountPerService: 12);
        _yamlDotNetSerializer = new SerializerBuilder().Build();
        _yamlDotNetDeserializer = new DeserializerBuilder().Build();
        _documentYaml = _yamlDotNetSerializer.Serialize(_document);
        _documentYamlWithUnknownKeys = _documentYaml + "unknownA: 1\nunknownB: {x: [1, 2]}\n";
        _reportingOptions = new SharpYaml.YamlSerializerOptions { UnmappedMemberCallback = _ => _unmappedCount++ };
    }

    [Benchmark(Baseline = true)]
    [BenchmarkCategory("Serialize_Poco")]
    public string SharpYaml_Serialize_Poco()
    {
        return SharpYaml.YamlSerializer.Serialize(_document);
    }

    [Benchmark]
    [BenchmarkCategory("Serialize_Poco")]
    public string YamlDotNet_Serialize_Poco()
    {
        return _yamlDotNetSerializer.Serialize(_document);
    }

    [Benchmark(Baseline = true)]
    [BenchmarkCategory("Deserialize_Poco")]
    public BenchmarkDocument SharpYaml_Deserialize_Poco()
    {
        return SharpYaml.YamlSerializer.Deserialize<BenchmarkDocument>(_documentYaml)!;
    }

    [Benchmark]
    [BenchmarkCategory("Deserialize_Poco")]
    public BenchmarkDocument YamlDotNet_Deserialize_Poco()
    {
        return _yamlDotNetDeserializer.Deserialize<BenchmarkDocument>(_documentYaml)!;
    }

    [Benchmark(Baseline = true)]
    [BenchmarkCategory("Deserialize_Poco_Unmapped")]
    public BenchmarkDocument SharpYaml_Deserialize_Poco_UnknownKeys()
    {
        return SharpYaml.YamlSerializer.Deserialize<BenchmarkDocument>(_documentYamlWithUnknownKeys)!;
    }

    [Benchmark]
    [BenchmarkCategory("Deserialize_Poco_Unmapped")]
    public BenchmarkDocument SharpYaml_Deserialize_Poco_UnknownKeys_WithCallback()
    {
        return SharpYaml.YamlSerializer.Deserialize<BenchmarkDocument>(_documentYamlWithUnknownKeys, _reportingOptions)!;
    }
}
