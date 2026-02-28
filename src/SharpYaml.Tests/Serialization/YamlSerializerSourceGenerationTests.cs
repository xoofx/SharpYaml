#nullable enable

using System;
using System.Text.Json.Serialization;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SharpYaml.Serialization;

namespace SharpYaml.Tests.Serialization;

internal sealed class GeneratedPerson
{
    [JsonPropertyName("first_name")]
    public string FirstName { get; set; } = string.Empty;

    public int Age { get; set; }
}

internal sealed class GeneratedContainer
{
    public GeneratedPerson? Person { get; set; }
}

internal sealed class GeneratedWithDefaultOptions
{
    public string DisplayName { get; set; } = string.Empty;

    public string? Optional { get; set; }
}

#pragma warning disable SYSLIB1224
[JsonSerializable(typeof(GeneratedPerson))]
[JsonSerializable(typeof(GeneratedContainer))]
internal partial class TestYamlSerializerContext : YamlSerializerContext
{
}

[JsonSourceGenerationOptions(
    WriteIndented = false,
    PropertyNameCaseInsensitive = true,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DictionaryKeyPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(GeneratedWithDefaultOptions))]
internal partial class TestYamlSerializerContextWithOptions : YamlSerializerContext
{
}
#pragma warning restore SYSLIB1224

[TestClass]
public class YamlSerializerSourceGenerationTests
{
    [TestMethod]
    public void GeneratedContextProvidesTypedMetadata()
    {
        var context = new TestYamlSerializerContext();
        var typeInfo = context.GetTypeInfo<GeneratedPerson>();
        var yaml = YamlSerializer.Serialize(
            new GeneratedPerson
            {
                FirstName = "Ada",
                Age = 37,
            },
            typeInfo);
        var person = YamlSerializer.Deserialize(yaml, typeInfo);

        StringAssert.Contains(yaml, "first_name");
        Assert.IsNotNull(person);
        Assert.AreEqual("Ada", person.FirstName);
        Assert.AreEqual(37, person.Age);
    }

    [TestMethod]
    public void GeneratedContextWorksAsResolver()
    {
        var context = new TestYamlSerializerContext();
        var yaml = YamlSerializer.Serialize(
            new GeneratedContainer
            {
                Person = new GeneratedPerson
                {
                    FirstName = "Ada",
                    Age = 37,
                },
            },
            typeof(GeneratedContainer),
            context.Options);
        var container = (GeneratedContainer?)YamlSerializer.Deserialize(yaml, typeof(GeneratedContainer), context.Options);

        Assert.IsNotNull(container);
        Assert.IsNotNull(container.Person);
        Assert.AreEqual("Ada", container.Person.FirstName);
        Assert.AreEqual(37, container.Person.Age);
    }

    [TestMethod]
    public void GeneratedContextDefaultAppliesJsonSourceGenerationOptions()
    {
        var context = TestYamlSerializerContextWithOptions.Default;
        var options = context.Options;

        Assert.IsFalse(options.WriteIndented);
        Assert.IsTrue(options.PropertyNameCaseInsensitive);
        Assert.AreEqual(YamlIgnoreCondition.WhenWritingNull, options.DefaultIgnoreCondition);
        Assert.AreSame(YamlNamingPolicy.CamelCase, options.PropertyNamingPolicy);
        Assert.AreSame(YamlNamingPolicy.CamelCase, options.DictionaryKeyPolicy);
        Assert.AreSame(context, options.TypeInfoResolver);

        var yaml = YamlSerializer.Serialize(
            new GeneratedWithDefaultOptions
            {
                DisplayName = "Ada",
                Optional = null,
            },
            typeof(GeneratedWithDefaultOptions),
            options);

        StringAssert.Contains(yaml, "displayName: Ada");
        Assert.IsFalse(yaml.Contains("optional:", StringComparison.Ordinal));
    }
}
