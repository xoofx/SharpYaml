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

    private ShapeShift.Yaml.YamlSerializer _shapeShiftSerializer = null!;

    private string _documentYaml = null!;
    private byte[] _documentUtf8 = null!;
    private string _shapeShiftYaml = null!;
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
        _documentUtf8 = System.Text.Encoding.UTF8.GetBytes(_documentYaml);

        // ShapeShift.Yaml reads a line-based subset of YAML (no flow collections, block scalars, anchors or tags),
        // so it deserializes the text it wrote itself, which has the same shape as SharpYaml's.
        _shapeShiftSerializer = new ShapeShift.Yaml.YamlSerializer();
        _shapeShiftYaml = _shapeShiftSerializer.Serialize(_document);
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

    [Benchmark]
    [BenchmarkCategory("Serialize_SourceGenerated")]
    public string ShapeShift_Serialize()
    {
        return _shapeShiftSerializer.Serialize(_document);
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

    [Benchmark]
    [BenchmarkCategory("Deserialize_SourceGenerated")]
    public BenchmarkDocument? ShapeShift_Deserialize()
    {
        return _shapeShiftSerializer.Deserialize<BenchmarkDocument>(_shapeShiftYaml);
    }

    [Benchmark(Baseline = true)]
    [BenchmarkCategory("Deserialize_SourceGenerated_Stream")]
    public BenchmarkDocument? SharpYaml_SourceGenerated_Deserialize_Stream()
    {
        return SharpYaml.YamlSerializer.Deserialize(new MemoryStream(_documentUtf8), _sharpYamlTypeInfo);
    }

    [Benchmark(Baseline = true)]
    [BenchmarkCategory("Serialize_SourceGenerated_Stream")]
    public long SharpYaml_SourceGenerated_Serialize_Stream()
    {
        var stream = new MemoryStream(_documentUtf8.Length + 16);
        SharpYaml.YamlSerializer.Serialize(stream, _document, _sharpYamlTypeInfo);
        return stream.Length;
    }

    [Benchmark(Baseline = true)]
    [BenchmarkCategory("Deserialize_SourceGenerated_MergeKeys")]
    public DatabaseConfiguration? SharpYaml_SourceGenerated_Deserialize_MergeKeys()
    {
        return SharpYaml.YamlSerializer.Deserialize(_mergeKeyYaml, _sharpYamlContext.DatabaseConfiguration);
    }
}
