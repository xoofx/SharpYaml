using System.Text.Json.Serialization;
using System.Numerics;
using SharpYaml;
using SharpYaml.Serialization;

var context = SmokeYamlContext.Default;
var typeInfo = context.SmokeConfig;

var yaml = YamlSerializer.Serialize(
    new SmokeConfig
    {
        Name = "aot",
        Enabled = true,
    },
    typeInfo);

var model = YamlSerializer.Deserialize(yaml, typeInfo);
if (model is null || model.Name != "aot" || !model.Enabled)
{
    return 1;
}

Console.WriteLine(yaml);

var populated = YamlSerializer.Deserialize("""
    root_object:
      - name: text 1
        value: other text 1
      - name: text 2
        value: other text 2
    ports: {1: 81}
    objects: {first: {name: nested, value: generated}}
    """, context.SmokePopulateConfig);
if (populated is null || populated.RootObject.Count != 2 ||
    populated.RootObject[0].Name != "text 1" || populated.RootObject[1].Value != "other text 2" ||
    populated.Ports[1] != 81 || populated.Objects["first"].Value != "generated")
{
    return 2;
}

var nullableOptions = new YamlSerializerOptions
{
    TypeInfoResolver = context,
    Converters = [new SmokeIntConverter()],
};
var nullableYaml = YamlSerializer.Serialize(new int?[] { 42, null, 42 }, nullableOptions);
var nullableValues = YamlSerializer.Deserialize<int?[]>(nullableYaml, nullableOptions);
if (nullableValues is not [42, null, 42] || !nullableYaml.Contains("custom"))
{
    return 3;
}

var skipOptions = new YamlSerializerOptions
{
    TypeInfoResolver = context,
    InvalidValueHandling = YamlInvalidValueHandling.Skip,
};
var skippedValues = YamlSerializer.Deserialize<int?[]>("[1, abc, null]", skipOptions);
if (skippedValues is not [1, null])
{
    return 4;
}

// The parser and the built-in untyped graph do not require reflected metadata.
var graphOptions = new YamlSerializerOptions { TypeInfoResolver = context };
var graph = YamlSerializer.Deserialize<object>("%RESERVED test\n---\nfirst: &name '123'\ncopy: *name\n? [a, b]\n: !!str 456", graphOptions);
if (graph is not Dictionary<object, object?> complexGraph || complexGraph["copy"] is not "123" ||
    complexGraph.Values.OfType<string>().Count(v => v == "456") != 1)
{
    return 5;
}
var graphYaml = YamlSerializer.Serialize(graph, graphOptions);
if (YamlSerializer.Deserialize<object>(graphYaml, graphOptions) is not Dictionary<object, object?> graphRoundTrip || graphRoundTrip.Count != 3)
{
    return 6;
}

var complexKeys = YamlSerializer.Deserialize<Dictionary<List<string>, string>>("? [a, b]\n: value", graphOptions);
if (complexKeys is null || complexKeys.Single().Key is not ["a", "b"] || complexKeys.Single().Value != "value")
{
    return 7;
}
var keyYaml = YamlSerializer.Serialize(complexKeys, graphOptions);
if (YamlSerializer.Deserialize<Dictionary<List<string>, string>>(keyYaml, graphOptions)?.Count != 1)
{
    return 8;
}

var largeInteger = BigInteger.Parse("123456789012345678901234567890", System.Globalization.CultureInfo.InvariantCulture);
var largeYaml = YamlSerializer.Serialize(largeInteger, graphOptions);
if (YamlSerializer.Deserialize<BigInteger>(largeYaml, graphOptions) != largeInteger ||
    YamlSerializer.Deserialize<object>(largeYaml, graphOptions) is not BigInteger largeUntyped || largeUntyped != largeInteger)
{
    return 9;
}
foreach (var handling in new[] { YamlReferenceHandling.Preserve, YamlReferenceHandling.PreserveMinimal })
{
    var preservedOptions = graphOptions with { ReferenceHandling = handling };
    var sharedMap = new Dictionary<string, object?> { ["k"] = "v" };
    object sharedGraph = new List<object?> { sharedMap, sharedMap };
    var sharedYaml = YamlSerializer.Serialize(sharedGraph, preservedOptions);
    var sharedResult = YamlSerializer.Deserialize<object>(sharedYaml, preservedOptions) as List<object?>;
    if (sharedResult is null || sharedResult[0] is not Dictionary<string, object?> first || first["k"] is not "v" ||
        !ReferenceEquals(sharedResult[0], sharedResult[1])) return 10;
}
var skippedRecursive = YamlSerializer.Deserialize("Unknown: &u {x: *u}\nname: kept", context.SmokeConfig);
if (skippedRecursive?.Name != "kept") return 11;
var bigNumber = YamlSerializer.Deserialize("Value: 123456789012345678901234567890", context.SmokeBigNumber);
if (bigNumber?.Value != largeInteger) return 12;

foreach (var identifier in new[] { "0xFFFFFFFFFFFFFFFF", "0xFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFF", "0x52908400098527886E0F7030069857D2E4169EE7", "0o7777777777777777777777777" })
{
    var identifierYaml = YamlSerializer.Serialize<object>(identifier, graphOptions);
    if (YamlSerializer.Deserialize<object>(identifierYaml, graphOptions) is not string identifierResult || identifierResult != identifier) return 13;
    object identifiers = new List<object?> { identifier, new Dictionary<string, object?> { ["value"] = identifier } };
    var identifiersYaml = YamlSerializer.Serialize(identifiers, graphOptions);
    if (YamlSerializer.Deserialize<object>(identifiersYaml, graphOptions) is not List<object?> identifiersResult ||
        identifiersResult[0] is not string firstIdentifier || firstIdentifier != identifier ||
        identifiersResult[1] is not Dictionary<string, object?> identifierMap || identifierMap["value"] is not string mappedIdentifier || mappedIdentifier != identifier) return 14;
}

return 0;

internal sealed class SmokeIntConverter : YamlConverter
{
    public override bool CanConvert(Type typeToConvert) => typeToConvert == typeof(int);

    public override object Read(YamlReader reader, Type typeToConvert)
    {
        if (typeToConvert != typeof(int) || reader.GetScalarValue() != "custom")
        {
            throw new InvalidOperationException("The underlying converter must only receive non-null int values.");
        }

        reader.Read();
        return 42;
    }

    public override void Write(YamlWriter writer, object? value)
    {
        if (value is not 42)
        {
            throw new InvalidOperationException("The underlying converter must only receive non-null int values.");
        }

        writer.WriteScalar("custom");
    }
}

internal sealed class SmokeConfig
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("enabled")]
    public bool Enabled { get; set; }
}

[JsonObjectCreationHandling(JsonObjectCreationHandling.Populate)]
internal sealed class SmokePopulateConfig
{
    [JsonPropertyName("root_object")]
    public IList<SmokePopulateItem> RootObject { get; } = new List<SmokePopulateItem>();

    [JsonPropertyName("ports")]
    public Dictionary<int, int> Ports { get; } = new() { [1] = 80 };

    [JsonPropertyName("objects")]
    public IDictionary<string, SmokePopulateItem> Objects { get; } = new Dictionary<string, SmokePopulateItem>();
}

internal sealed class SmokePopulateItem
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("value")]
    public string Value { get; set; } = string.Empty;
}

[YamlSerializable(typeof(SmokeConfig))]
[YamlSerializable(typeof(SmokePopulateConfig))]
[YamlSerializable(typeof(int?[]))]
[YamlSerializable(typeof(object))]
[YamlSerializable(typeof(BigInteger))]
[YamlSerializable(typeof(SmokeBigNumber))]
[YamlSerializable(typeof(Dictionary<List<string>, string>))]
internal partial class SmokeYamlContext : YamlSerializerContext
{
}

internal sealed class SmokeBigNumber
{
    public BigInteger Value { get; set; }
}
