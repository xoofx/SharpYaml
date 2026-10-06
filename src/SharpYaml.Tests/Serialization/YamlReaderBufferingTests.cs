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

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void IsDiscriminatorMember_IsOnlyTrueOnTheRootMappingOfTheBufferedNode(bool caseInsensitive)
    {
        var options = new SharpYaml.YamlSerializerOptions { PropertyNameCaseInsensitive = caseInsensitive };
        var reader = YamlReader.Create("$type: dog\nNested:\n  $type: cat\nItems:\n  - $type: fish\n$TYPE: other\n", options);
        Assert.IsTrue(reader.Read());

        // The reader the node is buffered from is not positioned in a polymorphic node.
        Assert.IsFalse(reader.IsDiscriminatorMember("$type"));
        var buffered = YamlReader.BufferCurrentNodeToStringAndFindDiscriminator(reader, "$type", out _);

        var bufferedReader = reader.CreateReader(buffered);
        Assert.IsTrue(bufferedReader.Read());
        Assert.IsFalse(bufferedReader.IsDiscriminatorMember("Other"));

        ReadKey(bufferedReader, "$type");
        Assert.IsTrue(bufferedReader.IsDiscriminatorMember("$type"));
        bufferedReader.Skip();

        ReadKey(bufferedReader, "Nested");
        Assert.IsFalse(bufferedReader.IsDiscriminatorMember("$type"));
        bufferedReader.Read();
        ReadKey(bufferedReader, "$type");
        Assert.IsFalse(bufferedReader.IsDiscriminatorMember("$type"));
        bufferedReader.Skip();
        Assert.AreEqual(YamlTokenType.EndMapping, bufferedReader.TokenType);
        bufferedReader.Read();

        ReadKey(bufferedReader, "Items");
        bufferedReader.Read();
        bufferedReader.Read();
        ReadKey(bufferedReader, "$type");
        Assert.IsFalse(bufferedReader.IsDiscriminatorMember("$type"));
        bufferedReader.Skip();
        bufferedReader.Read();
        Assert.AreEqual(YamlTokenType.EndSequence, bufferedReader.TokenType);
        bufferedReader.Read();

        // Back on the root mapping, the key is matched the same way the discriminator was looked up.
        ReadKey(bufferedReader, "$TYPE");
        Assert.AreEqual(caseInsensitive, bufferedReader.IsDiscriminatorMember("$TYPE"));

        // A node buffered without a discriminator has none.
        var plain = YamlReader.Create("$type: dog\n", options);
        Assert.IsTrue(plain.Read());
        var plainReader = plain.CreateReader(YamlReader.BufferCurrentNodeToString(plain));
        Assert.IsTrue(plainReader.Read());
        ReadKey(plainReader, "$type");
        Assert.IsFalse(plainReader.IsDiscriminatorMember("$type"));

        Assert.Throws<System.ArgumentNullException>(() => plainReader.IsDiscriminatorMember(null!));
    }

    // Moves from a mapping start or the end of the previous value onto the value of the expected key.
    private static void ReadKey(YamlReader reader, string expectedKey)
    {
        if (reader.TokenType == YamlTokenType.StartMapping)
        {
            reader.Read();
        }

        Assert.AreEqual(expectedKey, reader.GetScalarValue());
        reader.Read();
    }
}
