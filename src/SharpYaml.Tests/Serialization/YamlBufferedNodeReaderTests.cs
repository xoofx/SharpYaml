#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SharpYaml.Model;
using SharpYaml.Serialization;

namespace SharpYaml.Tests.Serialization;

/// <summary>
/// Covers the reader that polymorphic deserialization reads a value from once its type is known: it reads the events
/// kept from the node, and gives what the reader of the node written back to text gave.
/// </summary>
[TestClass]
public sealed class YamlBufferedNodeReaderTests
{
    public static IEnumerable<object[]> Modes => new[] { new object[] { false }, new object[] { true } };

    // Sequences whose items are buffered one by one.
    public static IEnumerable<object[]> Documents => new[]
    {
        "- $type: dog\n  Name: Rex\n- $type: cat\n  Name: Mittens\n",
        "- Name: Rex\n  $type: dog\n  Tags: [a, b, {c: d}]\n  Notes: |\n    line 1\n    line 2\n  Folded: >\n    a\n    b\n",
        "- &a {$type: dog, Name: &n Rex}\n- {$type: cat, Friend: *a, Name: *n}\n- *a\n",
        "- &x [1, 2]\n- $type: dog\n  Values: *x\n  Other: &y {k: v}\n  Again: *y\n  Name: &x again\n- *x\n",
        "- $type: dog\n  Nothing: ~\n  Empty:\n  Text: 'single'\n  More: \"double\\n\"\n  Tagged: !!str 12\n  Number: 0x1F\n  ? [complex, key]\n  : value\n",
        "- !dog\n  Name: Rex\n  Multi: a\n    b\n\n    c\n- plain scalar\n- [1, [2, [3]]]\n- {}\n- []\n- !!str\n",
        "- $type: [not, a, scalar]\n",
        "- Nested:\n    $type: cat\n  $type: dog\n  $type: second\n",
    }.Select(document => new object[] { document });

    [TestMethod]
    [DynamicData(nameof(Documents))]
    public void ReadsTheTokensOfTheNodeWrittenBackToText(string yaml)
    {
        foreach (var handling in new[] { YamlReferenceHandling.None, YamlReferenceHandling.Preserve })
        {
            var options = new YamlSerializerOptions { ReferenceHandling = handling };
            var fromEvents = YamlReader.Create(yaml, options);
            var fromText = YamlReader.Create(yaml, options);
            Assert.IsTrue(fromEvents.Read() && fromEvents.Read());
            Assert.IsTrue(fromText.Read() && fromText.Read());

            while (fromText.TokenType != YamlTokenType.EndSequence)
            {
                string expected;
                try
                {
                    var buffered = YamlReader.BufferCurrentNodeToStringAndFindDiscriminator(fromText, "$type", out var expectedDiscriminator);
                    expected = expectedDiscriminator + "\n" + Tokens(fromText.CreateReader(buffered));
                }
                catch (YamlException exception)
                {
                    // The same failure, where the node is in its document.
                    var actualException = Assert.Throws<YamlException>(() => fromEvents.BufferCurrentNodeAndFindDiscriminator("$type", out _));
                    Assert.AreEqual(exception.Message.Substring(exception.Message.IndexOf("): ", StringComparison.Ordinal)), actualException.Message.Substring(actualException.Message.IndexOf("): ", StringComparison.Ordinal)));
                    return;
                }

                var reader = fromEvents.BufferCurrentNodeAndFindDiscriminator("$type", out var discriminator);
                Assert.AreEqual(expected, discriminator + "\n" + Tokens(reader), $"{handling}\n{yaml}");
                Assert.AreEqual(fromText.TokenType, fromEvents.TokenType);
            }
        }
    }

    private static string Tokens(YamlReader reader)
    {
        var tokens = new List<string>();
        while (reader.Read())
        {
            tokens.Add($"{reader.TokenType}|{reader.ScalarValue}|{reader.Tag}|{reader.Anchor}|{reader.Alias}");
        }

        return string.Join("\n", tokens);
    }

    [TestMethod]
    public void FindsTheDiscriminatorOfTheRootMappingOnly()
    {
        static string? Find(string yaml, bool caseInsensitive = false)
        {
            var reader = YamlReader.Create(yaml, new YamlSerializerOptions { PropertyNameCaseInsensitive = caseInsensitive });
            Assert.IsTrue(reader.Read());
            reader.BufferCurrentNodeAndFindDiscriminator("$type", out var discriminator);
            Assert.AreEqual(YamlTokenType.None, reader.TokenType);
            return discriminator;
        }

        Assert.AreEqual("dog", Find("$type: dog\nName: Rex\n"));
        Assert.AreEqual("dog", Find("Name: Rex\n$type: dog\n"));
        Assert.AreEqual("dog", Find("Nested:\n  $type: cat\nItems:\n  - $type: fish\n$type: dog\n"));
        Assert.AreEqual("dog", Find("? {$type: cat}\n: {$type: fish}\n$type: dog\n"));
        Assert.AreEqual("dog", Find("$type: dog\n$type: cat\n"));
        Assert.AreEqual(string.Empty, Find("$type:\nName: Rex\n"));
        Assert.AreEqual("dog", Find("Name: $type\n$type: dog\n"));
        Assert.IsNull(Find("Name: $type\nOther: dog\n"));
        Assert.IsNull(Find("$TYPE: dog\n"));
        Assert.AreEqual("dog", Find("$TYPE: dog\n", caseInsensitive: true));
        Assert.IsNull(Find("[$type, dog]\n"));
        Assert.IsNull(Find("$type\n"));
    }

    [TestMethod]
    public void DiscriminatorThatIsNotAScalarIsRejectedWhereItIs()
    {
        var reader = YamlReader.Create("Name: Rex\n$type:\n  - dog\n");
        Assert.IsTrue(reader.Read());

        var exception = Assert.Throws<YamlException>(() => reader.BufferCurrentNodeAndFindDiscriminator("$type", out _));

        Assert.AreEqual(2, exception.Start.Line);
        Assert.Throws<ArgumentNullException>(() => reader.BufferCurrentNodeAndFindDiscriminator(null!, out _));
    }

    [TestMethod]
    public void ReportsThePositionsOfTheNodeInItsDocument()
    {
        var reader = YamlReader.Create("Animals:\n  - $type: dog\n    Name: Rex\n");
        Assert.IsTrue(reader.Read() && reader.Read() && reader.Read() && reader.Read());
        Assert.AreEqual(YamlTokenType.StartMapping, reader.TokenType);

        var buffered = reader.BufferCurrentNodeAndFindDiscriminator("$type", out _);

        Assert.IsTrue(buffered.Read());
        Assert.AreEqual(YamlTokenType.StartMapping, buffered.TokenType);
        Assert.AreEqual(1, buffered.Start.Line);
        Assert.AreEqual(4, buffered.Start.Column);

        Assert.IsTrue(buffered.Read() && buffered.Read() && buffered.Read() && buffered.Read());
        Assert.AreEqual("Rex", buffered.ScalarValue);
        Assert.AreEqual(2, buffered.Start.Line);
        Assert.AreEqual(10, buffered.Start.Column);
    }

    [TestMethod]
    public void KnowsTheDiscriminatorMemberOfItsRootMapping()
    {
        var reader = YamlReader.Create("$type: dog\nNested:\n  $type: cat\n");
        Assert.IsTrue(reader.Read());
        var buffered = reader.BufferCurrentNodeAndFindDiscriminator("$type", out _);

        Assert.IsTrue(buffered.Read() && buffered.Read() && buffered.Read());
        Assert.IsTrue(buffered.IsDiscriminatorMember("$type"));
        Assert.IsFalse(buffered.IsDiscriminatorMember("Nested"));

        // On the value of the nested key of the same name.
        Assert.IsTrue(buffered.Read() && buffered.Read() && buffered.Read() && buffered.Read());
        Assert.AreEqual("cat", buffered.ScalarValue);
        Assert.IsFalse(buffered.IsDiscriminatorMember("$type"));
    }

    [TestMethod]
    public void NodesBufferedOneAfterTheOtherAreIndependent()
    {
        // The readers of two nodes are read after both were buffered, and the first one after the second.
        var reader = YamlReader.Create("- {$type: dog, Name: Rex, Age: 3}\n- {$type: cat, Name: Tom, Age: 4}\n");
        Assert.IsTrue(reader.Read() && reader.Read());

        var first = reader.BufferCurrentNodeAndFindDiscriminator("$type", out var firstDiscriminator);
        var second = reader.BufferCurrentNodeAndFindDiscriminator("$type", out var secondDiscriminator);

        Assert.AreEqual("dog", firstDiscriminator);
        Assert.AreEqual("cat", secondDiscriminator);
        Assert.AreEqual("StartMapping||||\nScalar|$type|||\nScalar|cat|||\nScalar|Name|||\nScalar|Tom|||\nScalar|Age|||\nScalar|4|||\nEndMapping||||", Tokens(second));
        Assert.AreEqual("StartMapping||||\nScalar|$type|||\nScalar|dog|||\nScalar|Name|||\nScalar|Rex|||\nScalar|Age|||\nScalar|3|||\nEndMapping||||", Tokens(first));
    }

    [TestMethod]
    [DynamicData(nameof(Modes))]
    public void PolymorphicValuesAreReadWithTheirNestedValuesAndAliases(bool generated)
    {
        const string Yaml =
            "Animals:\n" +
            "  - $type: dog\n    Name: &rex Rex\n    Age: 3\n    Friends:\n      - {$type: cat, Name: Tom, Age: ~, Lives: 9}\n      - $type: dog\n        Name: *rex\n" +
            "  - Lives: &lives 7\n    Name: 'Kit'\n    $type: cat\n    Friends: []\n" +
            "  - &anchored\n    $type: dog\n    Name: Anchored\n    Friends:\n      - &inner {$type: cat, Name: Inner, Lives: *lives}\n";

        var zoo = Deserialize<BufferedZoo>(Yaml, options: null, generated)!;

        Assert.HasCount(3, zoo.Animals);
        var rex = (BufferedDog)zoo.Animals[0];
        Assert.AreEqual("Rex", rex.Name);
        Assert.AreEqual(3, rex.Age);
        Assert.HasCount(2, rex.Friends);
        var tom = (BufferedCat)rex.Friends[0];
        Assert.AreEqual("Tom", tom.Name);
        Assert.IsNull(tom.Age);
        Assert.AreEqual(9, tom.Lives);
        Assert.AreEqual("Rex", ((BufferedDog)rex.Friends[1]).Name);
        var kit = (BufferedCat)zoo.Animals[1];
        Assert.AreEqual("Kit", kit.Name);
        Assert.AreEqual(7, kit.Lives);
        var anchored = (BufferedDog)zoo.Animals[2];
        Assert.AreEqual("Anchored", anchored.Name);
        Assert.AreEqual(7, ((BufferedCat)anchored.Friends[0]).Lives);
    }

    [TestMethod]
    public void AliasThatFollowsABufferedNodeIsReplayed()
    {
        // The aliases of the node are passed through while it is buffered; the token after the node is not part of it.
        const string Yaml = "- &a {$type: dog, Name: Rex}\n- *a\n";

        var reader = YamlReader.Create(Yaml);
        Assert.IsTrue(reader.Read() && reader.Read());
        reader.BufferCurrentNodeAndFindDiscriminator("$type", out _);
        Assert.AreEqual(YamlTokenType.StartMapping, reader.TokenType);
        Assert.IsNull(reader.Anchor);

        var textReader = YamlReader.Create(Yaml);
        Assert.IsTrue(textReader.Read() && textReader.Read());
        YamlReader.BufferCurrentNodeToStringAndFindDiscriminator(textReader, "$type", out _);
        Assert.AreEqual(YamlTokenType.StartMapping, textReader.TokenType);

        var plainReader = YamlReader.Create(Yaml);
        Assert.IsTrue(plainReader.Read() && plainReader.Read());
        YamlReader.BufferCurrentNodeToString(plainReader);
        Assert.AreEqual(YamlTokenType.StartMapping, plainReader.TokenType);
    }

    [TestMethod]
    [DynamicData(nameof(Modes))]
    public void AliasToAPolymorphicValueIsRead(bool generated)
    {
        const string Yaml = "Animals:\n  - &rex {$type: dog, Name: Rex, Age: 3}\n  - *rex\n  - &tom\n    $type: cat\n    Name: Tom\n    Lives: 9\n    Friends: [*rex]\n  - *tom\n  - *rex\n";

        foreach (var handling in new[] { YamlReferenceHandling.None, YamlReferenceHandling.Preserve })
        {
            var zoo = Deserialize<BufferedZoo>(Yaml, new YamlSerializerOptions { ReferenceHandling = handling }, generated)!;

            CollectionAssert.AreEqual(new[] { "Rex", "Rex", "Tom", "Tom", "Rex" }, zoo.Animals.Select(animal => animal.Name).ToArray(), handling.ToString());
            CollectionAssert.AreEqual(
                new[] { typeof(BufferedDog), typeof(BufferedDog), typeof(BufferedCat), typeof(BufferedCat), typeof(BufferedDog) },
                zoo.Animals.Select(animal => animal.GetType()).ToArray(),
                handling.ToString());
            Assert.AreEqual(9, ((BufferedCat)zoo.Animals[3]).Lives);
            Assert.AreEqual("Rex", zoo.Animals[3].Friends[0].Name);
            Assert.AreEqual(handling == YamlReferenceHandling.Preserve, ReferenceEquals(zoo.Animals[0], zoo.Animals[1]), handling.ToString());
        }
    }

    [TestMethod]
    [DynamicData(nameof(Modes))]
    public void InvalidValueOfAPolymorphicValueIsReportedWhereItIsInTheDocument(bool generated)
    {
        const string Yaml = "Animals:\n  - $type: dog\n    Name: Rex\n  - $type: cat\n    Name: Tom\n    Lives: many\n";

        var exception = Assert.Throws<YamlException>(() => Deserialize<BufferedZoo>(Yaml, options: null, generated));

        Assert.AreEqual(5, exception.Start.Line);
        Assert.AreEqual(11, exception.Start.Column);
    }

    [TestMethod]
    [DynamicData(nameof(Modes))]
    public void UnmappedMembersOfAPolymorphicValueAreReportedWithoutItsDiscriminator(bool generated)
    {
        var members = new List<YamlUnmappedMember>();
        var options = new YamlSerializerOptions { UnmappedMemberCallback = members.Add };

        var zoo = Deserialize<BufferedZoo>("Animals:\n  - $type: dog\n    Name: Rex\n    Extra: 1\n    Other: 2\n    <<: {Extra: merged, Other: merged, $type: cat, Age: 5}\n", options, generated)!;

        var rex = (BufferedDog)zoo.Animals[0];
        Assert.AreEqual("Rex", rex.Name);
        Assert.AreEqual(5, rex.Age);
        CollectionAssert.AreEqual(new[] { "Extra", "Other" }, members.Select(member => member.Name).ToArray());
        CollectionAssert.AreEqual(new[] { "1", "2" }, members.Select(member => ((YamlValue)member.Value!).Value).ToArray());
        Assert.AreEqual("$.Animals[0].Extra", members[0].Path);
    }

    private static T? Deserialize<T>(string yaml, YamlSerializerOptions? options, bool generated)
    {
        if (!generated)
        {
            return YamlSerializer.Deserialize<T>(yaml, options);
        }

        var context = options is null ? BufferedNodeContext.Default : new BufferedNodeContext(options);
        return (T?)YamlSerializer.Deserialize(yaml, typeof(T), context);
    }
}

public sealed class BufferedZoo
{
    public List<BufferedAnimal> Animals { get; set; } = new();
}

[YamlPolymorphic]
[YamlDerivedType(typeof(BufferedDog), "dog")]
[YamlDerivedType(typeof(BufferedCat), "cat")]
public abstract class BufferedAnimal
{
    public string Name { get; set; } = string.Empty;

    public int? Age { get; set; }

    public List<BufferedAnimal> Friends { get; set; } = new();
}

public sealed class BufferedDog : BufferedAnimal
{
}

public sealed class BufferedCat : BufferedAnimal
{
    public int Lives { get; set; }
}

[YamlSerializable(typeof(BufferedZoo))]
internal partial class BufferedNodeContext : YamlSerializerContext
{
    public BufferedNodeContext()
    {
    }

    public BufferedNodeContext(YamlSerializerOptions options) : base(options)
    {
    }
}
