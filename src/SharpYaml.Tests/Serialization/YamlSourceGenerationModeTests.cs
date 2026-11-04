#nullable enable

using System;
using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SharpYaml.Serialization;

namespace SharpYaml.Tests.Serialization;

internal sealed class ModeAddress
{
    public string Street { get; set; } = string.Empty;
}

internal sealed class ModePerson
{
    public string Name { get; set; } = string.Empty;

    public int Age { get; set; }

    public List<ModeAddress> Addresses { get; set; } = new();

    public Dictionary<string, string> Tags { get; set; } = new();
}

internal sealed class ModeCtorPerson
{
    [YamlConstructor]
    public ModeCtorPerson(string name, int age)
    {
        Name = name;
        Age = age;
    }

    public string Name { get; }

    public int Age { get; }
}

[YamlSourceGenerationOptions(GenerationMode = YamlSourceGenerationMode.Deserialization)]
[YamlSerializable(typeof(ModePerson))]
[YamlSerializable(typeof(ModeCtorPerson))]
internal partial class DeserializationOnlyContext : YamlSerializerContext
{
}

[YamlSourceGenerationOptions(GenerationMode = YamlSourceGenerationMode.Serialization)]
[YamlSerializable(typeof(ModePerson))]
[YamlSerializable(typeof(ModeCtorPerson))]
internal partial class SerializationOnlyContext : YamlSerializerContext
{
}

[YamlSourceGenerationOptions(GenerationMode = YamlSourceGenerationMode.Serialization | YamlSourceGenerationMode.Deserialization)]
[YamlSerializable(typeof(ModePerson))]
internal partial class BothDirectionsContext : YamlSerializerContext
{
}

[TestClass]
public class YamlSourceGenerationModeTests
{
    private const string PersonYaml = """
        Name: Ada
        Age: 37
        Addresses:
        - Street: Main
        - Street: Side
        Tags:
          team: math
        """;

    [TestMethod]
    public void DeserializationMode_ReadsObjectsListsAndDictionaries()
    {
        var person = YamlSerializer.Deserialize(PersonYaml, DeserializationOnlyContext.Default.ModePerson);

        Assert.IsNotNull(person);
        Assert.AreEqual("Ada", person.Name);
        Assert.AreEqual(37, person.Age);
        Assert.AreEqual(2, person.Addresses.Count);
        Assert.AreEqual("Side", person.Addresses[1].Street);
        Assert.AreEqual("math", person.Tags["team"]);
    }

    [TestMethod]
    public void DeserializationMode_ReadsParameterizedConstructorTypes()
    {
        var person = YamlSerializer.Deserialize("""
            Name: Ada
            Age: 37
            """, DeserializationOnlyContext.Default.ModeCtorPerson);

        Assert.IsNotNull(person);
        Assert.AreEqual("Ada", person.Name);
        Assert.AreEqual(37, person.Age);
    }

    [TestMethod]
    public void DeserializationMode_SerializingThrowsNotSupported()
    {
        var exception = Assert.Throws<NotSupportedException>(
            () => YamlSerializer.Serialize(new ModePerson { Name = "Ada" }, DeserializationOnlyContext.Default.ModePerson));

        StringAssert.Contains(exception.Message, "deserialization only");
    }

    [TestMethod]
    public void SerializationMode_WritesObjects()
    {
        var yaml = YamlSerializer.Serialize(
            new ModePerson { Name = "Ada", Age = 37, Addresses = { new ModeAddress { Street = "Main" } } },
            SerializationOnlyContext.Default.ModePerson);

        StringAssert.Contains(yaml, "Name: Ada");
        StringAssert.Contains(yaml, "Street: Main");
    }

    [TestMethod]
    public void SerializationMode_DeserializingThrowsNotSupported()
    {
        var exception = Assert.Throws<NotSupportedException>(
            () => YamlSerializer.Deserialize(PersonYaml, SerializationOnlyContext.Default.ModePerson));

        StringAssert.Contains(exception.Message, "serialization only");
    }

    [TestMethod]
    public void BothFlags_RoundTrip()
    {
        var original = new ModePerson { Name = "Ada", Age = 37, Addresses = { new ModeAddress { Street = "Main" } } };

        var yaml = YamlSerializer.Serialize(original, BothDirectionsContext.Default.ModePerson);
        var copy = YamlSerializer.Deserialize(yaml, BothDirectionsContext.Default.ModePerson);

        Assert.IsNotNull(copy);
        Assert.AreEqual("Ada", copy.Name);
        Assert.AreEqual("Main", copy.Addresses[0].Street);
    }
}
