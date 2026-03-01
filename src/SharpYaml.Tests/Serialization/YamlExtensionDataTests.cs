using System.Collections.Generic;
using System.Text.Json.Serialization;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SharpYaml.Model;
using SharpYaml.Serialization;

namespace SharpYaml.Tests.Serialization;

[TestClass]
public sealed class YamlExtensionDataTests
{
    [TestMethod]
    public void Deserialize_CapturesUnknownKeysIntoDictionary()
    {
        var value = YamlSerializer.Deserialize<DictionaryExtensionDataModel>("A: 1\nB: 2\nC: test\n")!;

        Assert.AreEqual(1, value.A);
        Assert.AreEqual(2L, value.Extra["B"]);
        Assert.AreEqual("test", value.Extra["C"]);
    }

    [TestMethod]
    public void Serialize_EmitsExtensionDataDictionaryEntries()
    {
        var value = new DictionaryExtensionDataModel
        {
            A = 1,
            Extra = new Dictionary<string, object?> { ["B"] = 2, ["C"] = "test" },
        };

        var yaml = YamlSerializer.Serialize(value);

        StringAssert.Contains(yaml, "A: 1");
        StringAssert.Contains(yaml, "B: 2");
        StringAssert.Contains(yaml, "C: test");
    }

    [TestMethod]
    public void Deserialize_JsonExtensionDataAttribute_IsRecognized()
    {
        var value = YamlSerializer.Deserialize<JsonDictionaryExtensionDataModel>("A: 1\nB: 2\n")!;

        Assert.AreEqual(1, value.A);
        Assert.AreEqual(2L, value.Extra["B"]);
    }

    [TestMethod]
    public void Deserialize_CapturesUnknownKeysIntoYamlMapping()
    {
        var value = YamlSerializer.Deserialize<MappingExtensionDataModel>("A: 1\nB: 2\n")!;

        Assert.AreEqual(1, value.A);

        YamlElement? captured = null;
        foreach (var pair in value.Extra)
        {
            if (pair.Key is YamlValue keyValue && keyValue.Value == "B")
            {
                captured = pair.Value;
                break;
            }
        }

        Assert.IsNotNull(captured);
        Assert.IsInstanceOfType(captured, typeof(YamlValue));
        Assert.AreEqual("2", ((YamlValue)captured!).Value);
    }

    [TestMethod]
    public void Serialize_EmitsExtensionDataMappingEntries()
    {
        var value = new MappingExtensionDataModel
        {
            A = 1,
            Extra = new YamlMapping(),
        };

        value.Extra.Add(new YamlValue("B"), new YamlValue(2));

        var yaml = YamlSerializer.Serialize(value);

        StringAssert.Contains(yaml, "A: 1");
        StringAssert.Contains(yaml, "B:");
        StringAssert.Contains(yaml, "2");
    }

    private sealed class DictionaryExtensionDataModel
    {
        public int A { get; set; }

        [YamlExtensionData]
        public Dictionary<string, object?> Extra { get; set; } = new();
    }

    private sealed class JsonDictionaryExtensionDataModel
    {
        public int A { get; set; }

        [JsonExtensionData]
        public Dictionary<string, object?> Extra { get; set; } = new();
    }

    private sealed class MappingExtensionDataModel
    {
        public int A { get; set; }

        [YamlExtensionData]
        public YamlMapping Extra { get; set; } = new();
    }
}
