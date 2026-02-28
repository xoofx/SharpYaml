#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace SharpYaml.Tests.Serialization;

[TestClass]
public sealed class YamlCollectionConverterTests
{
    private sealed class EnumerableModel
    {
        public IEnumerable<int> Values { get; set; } = Array.Empty<int>();
    }

    [TestMethod]
    public void Array_RoundTrip()
    {
        var data = new[] { 1, 2, 3 };
        var yaml = YamlSerializer.Serialize(data);
        var roundTrip = YamlSerializer.Deserialize<int[]>(yaml);

        Assert.IsNotNull(roundTrip);
        CollectionAssert.AreEqual(data, roundTrip);
    }

    [TestMethod]
    public void IEnumerable_RoundTripThroughObjectProperty()
    {
        var model = new EnumerableModel { Values = new[] { 1, 2, 3 } };
        var yaml = YamlSerializer.Serialize(model);

        var deserialized = YamlSerializer.Deserialize<EnumerableModel>(yaml);
        Assert.IsNotNull(deserialized);

        var values = deserialized.Values.ToArray();
        CollectionAssert.AreEqual(new[] { 1, 2, 3 }, values);
    }

    [TestMethod]
    public void DictionaryKeyPolicy_AppliesToSerializedKeys()
    {
        var options = new YamlSerializerOptions { DictionaryKeyPolicy = YamlNamingPolicy.CamelCase };
        var yaml = YamlSerializer.Serialize(
            new Dictionary<string, int>
            {
                ["MyKey"] = 1,
                ["OtherKey"] = 2,
            },
            options);

        StringAssert.Contains(yaml, "myKey: 1");
        StringAssert.Contains(yaml, "otherKey: 2");
        Assert.IsFalse(yaml.Contains("MyKey:", StringComparison.Ordinal));
        Assert.IsFalse(yaml.Contains("OtherKey:", StringComparison.Ordinal));
    }

    [TestMethod]
    public void Indentation_RespectsIndentSize()
    {
        var yaml = YamlSerializer.Serialize(
            new Dictionary<string, Dictionary<string, int>>
            {
                ["outer"] = new Dictionary<string, int> { ["inner"] = 1 },
            },
            new YamlSerializerOptions { IndentSize = 4 });

        StringAssert.Contains(yaml, "outer:\n    inner: 1\n");
    }
}

