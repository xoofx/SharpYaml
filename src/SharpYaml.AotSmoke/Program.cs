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
return 0;

internal sealed class SmokeConfig
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("enabled")]
    public bool Enabled { get; set; }
}

[JsonSerializable(typeof(SmokeConfig))]
internal partial class SmokeYamlContext : YamlSerializerContext
{
}
