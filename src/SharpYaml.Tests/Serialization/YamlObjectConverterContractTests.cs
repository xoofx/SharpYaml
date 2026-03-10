#nullable enable

using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SharpYaml.Serialization;

namespace SharpYaml.Tests.Serialization;

[TestClass]
public sealed class YamlObjectConverterContractTests
{
    private sealed class FieldIncludedModel
    {
        [YamlInclude]
        [YamlPropertyName("age")]
        private int _age;

        public int Age => _age;

        public FieldIncludedModel()
        {
        }

        public FieldIncludedModel(int age)
        {
            _age = age;
        }
    }

    private sealed class IgnoredModel
    {
        public int Keep { get; set; }

        [YamlIgnore]
        public int Skip { get; set; }
    }

    private sealed class DefaultIgnoredModel
    {
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public int Count { get; set; }
    }

    private sealed class Person
    {
        public string FirstName { get; set; } = string.Empty;
    }

    [JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
    private sealed class StrictPerson
    {
        public string FirstName { get; set; } = string.Empty;
    }

    [JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
    private sealed class StrictExtensionDataPerson
    {
        public string FirstName { get; set; } = string.Empty;

        [YamlExtensionData]
        public Dictionary<string, object?> Extra { get; set; } = new();
    }

    private sealed class NullIgnoreModel
    {
        public string? Nick { get; set; }

        public string Name { get; set; } = string.Empty;
    }

    private abstract class AbstractBase
    {
        public int X { get; set; }
    }

    private sealed class NoDefaultCtor
    {
        public NoDefaultCtor(int value) => Value = value;

        public int Value { get; }
    }

    private sealed class MultiplePublicCtors
    {
        public MultiplePublicCtors(int value) => Value = value;

        public MultiplePublicCtors(string name) => Name = name;

        public int Value { get; }

        public string? Name { get; }
    }

    [TestMethod]
    public void IncludedField_IsSerializedAndDeserialized()
    {
        var yaml = YamlSerializer.Serialize(new FieldIncludedModel(37));
        StringAssert.Contains(yaml, "age: 37");

        var roundTrip = YamlSerializer.Deserialize<FieldIncludedModel>("age: 41\n");
        Assert.IsNotNull(roundTrip);
        Assert.AreEqual(41, roundTrip.Age);
    }

    [TestMethod]
    public void YamlIgnore_SkipsMemberForWriteAndRead()
    {
        var yaml = YamlSerializer.Serialize(new IgnoredModel { Keep = 1, Skip = 2 });
        StringAssert.Contains(yaml, "Keep: 1");
        Assert.IsFalse(yaml.Contains("Skip:", StringComparison.Ordinal));

        var deserialized = YamlSerializer.Deserialize<IgnoredModel>("Keep: 1\nSkip: 999\n");
        Assert.IsNotNull(deserialized);
        Assert.AreEqual(1, deserialized.Keep);
        Assert.AreEqual(0, deserialized.Skip);
    }

    [TestMethod]
    public void JsonIgnore_WhenWritingDefault_HidesDefaultValue()
    {
        var yaml = YamlSerializer.Serialize(new DefaultIgnoredModel { Count = 0 });
        Assert.AreEqual("{}\n", yaml);

        var yaml2 = YamlSerializer.Serialize(new DefaultIgnoredModel { Count = 2 });
        StringAssert.Contains(yaml2, "Count: 2");
    }

    [TestMethod]
    public void DefaultIgnoreCondition_WhenWritingNull_HidesNullMembers()
    {
        var yaml = YamlSerializer.Serialize(
            new NullIgnoreModel { Nick = null, Name = "Ada" },
            new YamlSerializerOptions { DefaultIgnoreCondition = YamlIgnoreCondition.WhenWritingNull });

        StringAssert.Contains(yaml, "Name: Ada");
        Assert.IsFalse(yaml.Contains("Nick:", StringComparison.Ordinal));
    }

    [TestMethod]
    public void PropertyNameCaseInsensitive_AllowsMismatchedCasing()
    {
        var person = YamlSerializer.Deserialize<Person>(
            "firstname: Ada\n",
            new YamlSerializerOptions { PropertyNameCaseInsensitive = true });

        Assert.IsNotNull(person);
        Assert.AreEqual("Ada", person.FirstName);
    }

    [TestMethod]
    public void UnmappedMembers_AreSkippedByDefault()
    {
        var person = YamlSerializer.Deserialize<Person>("FirstName: Ada\nLastName: Lovelace\n");

        Assert.IsNotNull(person);
        Assert.AreEqual("Ada", person.FirstName);
    }

    [TestMethod]
    public void UnmappedMembers_CanBeDisallowedViaOptions()
    {
        var options = new YamlSerializerOptions { UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow };

        var exception = Assert.Throws<YamlException>(() => YamlSerializer.Deserialize<Person>("FirstName: Ada\nLastName: Lovelace\n", options));

        StringAssert.Contains(exception.Message, "LastName");
        StringAssert.Contains(exception.Message, typeof(Person).ToString());
    }

    [TestMethod]
    public void JsonUnmappedMemberHandlingAttribute_CanDisallowUnknownMembers()
    {
        var exception = Assert.Throws<YamlException>(() => YamlSerializer.Deserialize<StrictPerson>("FirstName: Ada\nLastName: Lovelace\n"));

        StringAssert.Contains(exception.Message, "LastName");
        StringAssert.Contains(exception.Message, typeof(StrictPerson).ToString());
    }

    [TestMethod]
    public void JsonUnmappedMemberHandling_DoesNotConflictWithExtensionData()
    {
        var person = YamlSerializer.Deserialize<StrictExtensionDataPerson>("FirstName: Ada\nLastName: Lovelace\n");

        Assert.IsNotNull(person);
        Assert.AreEqual("Ada", person.FirstName);
        Assert.AreEqual("Lovelace", person.Extra["LastName"]);
    }

    [TestMethod]
    public void ContractErrors_AreWrappedInYamlExceptionWithContext()
    {
        var options = new YamlSerializerOptions { SourceName = "model.yaml" };

        var ex1 = Assert.Throws<YamlException>(() => YamlSerializer.Deserialize<AbstractBase>("X: 1\n", options));
        Assert.AreEqual("model.yaml", ex1.SourceName);
        StringAssert.Contains(ex1.Message, "cannot be instantiated");

        var value = YamlSerializer.Deserialize<NoDefaultCtor>("Value: 1\n", options);
        Assert.IsNotNull(value);
        Assert.AreEqual(1, value.Value);

        var ex2 = Assert.Throws<YamlException>(() => YamlSerializer.Deserialize<MultiplePublicCtors>("Value: 1\n", options));
        Assert.AreEqual("model.yaml", ex2.SourceName);
        StringAssert.Contains(ex2.Message, "multiple public constructors");
    }
}
