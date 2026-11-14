using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SharpYaml.Serialization;

namespace SharpYaml.Tests.Serialization;

[TestClass]
public sealed class Yaml122BufferingTests
{
    [TestMethod]
    public void BufferingPreservesScalarStylesTagsAndComplexKeys()
    {
        const string yaml = "quoted: '123'\nblock: |\n  456\ntagged: !!str 789\nempty: !!str\n? [a, b]\n: value";
        var reader = YamlReader.Create(yaml);
        Assert.IsTrue(reader.Read());
        var buffered = YamlReader.BufferCurrentNodeToString(reader);
        var values = (Dictionary<object, object?>)YamlSerializer.Deserialize<object>(buffered)!;
        Assert.AreEqual("123", values["quoted"]);
        Assert.AreEqual("456\n", values["block"]);
        Assert.AreEqual("789", values["tagged"]);
        Assert.AreEqual("", values["empty"]);
        var key = values.Keys.OfType<List<object?>>().Single();
        Assert.AreEqual("value", values[key]);
    }

    [TestMethod]
    [DataRow("!local,name[brackets]!", "!local,name[brackets]!")]
    [DataRow("tag:example.org,2026:type", "tag:example.org,2026:type")]
    [DataRow("!!str", "tag:yaml.org,2002:str")]
    public void WriterEmitsParseableExpandedAndLocalTags(string tag, string expected)
    {
        using var output = new StringWriter();
        var writer = new YamlWriter(output, new YamlSerializerOptions());
        writer.WriteTag(tag);
        writer.WriteScalar("value");
        var reader = YamlReader.Create(output.ToString());
        Assert.IsTrue(reader.Read());
        Assert.AreEqual(expected, reader.Tag);
        Assert.AreEqual("value", reader.ScalarValue);
    }

    [TestMethod]
    public void ModelWritingPreservesScalarSemanticsInsideComplexKeys()
    {
        const string yaml = "? ['123', !!str 456]\n: '789'";
        var model = YamlSerializer.Deserialize<SharpYaml.Model.YamlNode>(yaml)!;
        var roundTrip = YamlSerializer.Serialize(model);
        var mapping = (Dictionary<object, object?>)YamlSerializer.Deserialize<object>(roundTrip)!;
        var key = mapping.Keys.OfType<List<object?>>().Single();
        CollectionAssert.AreEqual(new[] { "123", "456" }, key);
        Assert.AreEqual("789", mapping[key]);
    }

    [TestMethod]
    [DataRow("&key $type: dog\nName: Rex")]
    [DataRow("!!str $type: dog\nName: Rex")]
    public void BufferingFindsDiscriminatorOnKeysWithProperties(string yaml)
    {
        var reader = YamlReader.Create(yaml);
        Assert.IsTrue(reader.Read());
        var buffered = YamlReader.BufferCurrentNodeToStringAndFindDiscriminator(reader, "$type", out var discriminator);
        Assert.AreEqual("dog", discriminator);
        Assert.IsNotNull(YamlSerializer.Deserialize<object>(buffered));
    }
}
