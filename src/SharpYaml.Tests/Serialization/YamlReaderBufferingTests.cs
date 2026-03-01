#nullable enable

using Microsoft.VisualStudio.TestTools.UnitTesting;
using SharpYaml.Serialization;

namespace SharpYaml.Tests.Serialization;

[TestClass]
public sealed class YamlReaderBufferingTests
{
    [TestMethod]
    public void BufferCurrentNodeToStringAndFindDiscriminator_ExtractsValue_AndAdvancesReader()
    {
        var yaml = "- $type: dog\n  Name: Rex\n- $type: cat\n  Name: Mittens\n";
        var options = new SharpYaml.YamlSerializerOptions { PropertyNameCaseInsensitive = false };

        var reader = YamlReader.Create(yaml, options);
        Assert.IsTrue(reader.Read());
        Assert.AreEqual(YamlTokenType.StartSequence, reader.TokenType);

        Assert.IsTrue(reader.Read());
        Assert.AreEqual(YamlTokenType.StartMapping, reader.TokenType);

        var buffered = YamlReader.BufferCurrentNodeToStringAndFindDiscriminator(reader, "$type", out var discriminator);
        Assert.AreEqual("dog", discriminator);
        StringAssert.Contains(buffered, "$type: dog");
        StringAssert.Contains(buffered, "Name: Rex");

        // Reader should be positioned at the next sequence item (second mapping).
        Assert.AreEqual(YamlTokenType.StartMapping, reader.TokenType);
        var buffered2 = YamlReader.BufferCurrentNodeToStringAndFindDiscriminator(reader, "$type", out var discriminator2);
        Assert.AreEqual("cat", discriminator2);
        StringAssert.Contains(buffered2, "$type: cat");
        StringAssert.Contains(buffered2, "Name: Mittens");

        Assert.AreEqual(YamlTokenType.EndSequence, reader.TokenType);
    }

    [TestMethod]
    public void BufferCurrentNodeToStringAndFindDiscriminator_RespectsCaseInsensitiveOption()
    {
        var yaml = "- $TYPE: dog\n  Name: Rex\n";
        var options = new SharpYaml.YamlSerializerOptions { PropertyNameCaseInsensitive = true };

        var reader = YamlReader.Create(yaml, options);
        Assert.IsTrue(reader.Read());
        Assert.AreEqual(YamlTokenType.StartSequence, reader.TokenType);
        Assert.IsTrue(reader.Read());
        Assert.AreEqual(YamlTokenType.StartMapping, reader.TokenType);

        _ = YamlReader.BufferCurrentNodeToStringAndFindDiscriminator(reader, "$type", out var discriminator);
        Assert.AreEqual("dog", discriminator);
    }
}
