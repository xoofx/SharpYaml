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

    [GlobalSetup]
    public void Setup()
    {
        _document = BenchmarkDataFactory.CreateDocument(serviceCount: 200, endpointCountPerService: 12);

        _sharpYamlContext = SharpYamlBenchmarkContext.Default;
        _sharpYamlTypeInfo = (SharpYaml.YamlTypeInfo<BenchmarkDocument>)_sharpYamlContext.GetTypeInfo(typeof(BenchmarkDocument), _sharpYamlContext.Options)!;

        var yamlDotNetStaticContext = new YamlDotNetBenchmarkContext();
        _yamlDotNetStaticSerializer = new StaticSerializerBuilder(yamlDotNetStaticContext).Build();
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
}
