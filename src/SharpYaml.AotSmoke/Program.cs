using System.Text.Json.Serialization;
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
    """, context.SmokePopulateConfig);
if (populated is null || populated.RootObject.Count != 2 ||
    populated.RootObject[0].Name != "text 1" || populated.RootObject[1].Value != "other text 2")
{
    return 2;
}

return 0;

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
internal partial class SmokeYamlContext : YamlSerializerContext
{
}
