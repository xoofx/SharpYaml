using BenchmarkDotNet.Attributes;
using YamlDotNet.Serialization;

namespace SharpYaml.Benchmarks;

[MemoryDiagnoser]
public class SourceGeneratedBenchmarks
{
    private BenchmarkDocument _document = null!;

    private SharpYamlBenchmarkContext _sharpYamlContext = null!;
    private SharpYaml.YamlTypeInfo<BenchmarkDocument> _sharpYamlTypeInfo = null!;

    private ISerializer _yamlDotNetStaticSerializer = null!;
    private IDeserializer _yamlDotNetStaticDeserializer = null!;

    private string _documentYaml = null!;
    private string _mergeKeyYaml = null!;

    [GlobalSetup]
    public void Setup()
    {
        _document = BenchmarkDataFactory.CreateDocument(serviceCount: 200, endpointCountPerService: 12);

        _sharpYamlContext = SharpYamlBenchmarkContext.Default;
        _sharpYamlTypeInfo = _sharpYamlContext.BenchmarkDocument;

        var yamlDotNetStaticContext = new YamlDotNetBenchmarkContext();
        _yamlDotNetStaticSerializer = new StaticSerializerBuilder(yamlDotNetStaticContext).Build();
        _yamlDotNetStaticDeserializer = new StaticDeserializerBuilder(yamlDotNetStaticContext).Build();

        _documentYaml = SharpYaml.YamlSerializer.Serialize(_document, _sharpYamlTypeInfo);
        _mergeKeyYaml = """
            <<:
              Host: db.internal
              Port: 5432
              User: app
            Password: secret
            Schema: public
            UseSsl: true
            """;
    }

    [Benchmark(Baseline = true)]
    [BenchmarkCategory("Serialize_SourceGenerated")]
    public string SharpYaml_SourceGenerated_Serialize()
    {
        return SharpYaml.YamlSerializer.Serialize(_document, _sharpYamlTypeInfo);
    }

    [Benchmark]
    [BenchmarkCategory("Serialize_SourceGenerated")]
    public string YamlDotNet_StaticGenerator_Serialize()
    {
        return _yamlDotNetStaticSerializer.Serialize(_document);
    }

    [Benchmark(Baseline = true)]
    [BenchmarkCategory("Deserialize_SourceGenerated")]
    public BenchmarkDocument? SharpYaml_SourceGenerated_Deserialize()
    {
        return SharpYaml.YamlSerializer.Deserialize(_documentYaml, _sharpYamlTypeInfo);
    }

    [Benchmark]
    [BenchmarkCategory("Deserialize_SourceGenerated")]
    public BenchmarkDocument YamlDotNet_StaticGenerator_Deserialize()
    {
        return _yamlDotNetStaticDeserializer.Deserialize<BenchmarkDocument>(_documentYaml);
    }

    [Benchmark(Baseline = true)]
    [BenchmarkCategory("Deserialize_SourceGenerated_MergeKeys")]
    public DatabaseConfiguration? SharpYaml_SourceGenerated_Deserialize_MergeKeys()
    {
        return SharpYaml.YamlSerializer.Deserialize(_mergeKeyYaml, _sharpYamlContext.DatabaseConfiguration);
    }
}
