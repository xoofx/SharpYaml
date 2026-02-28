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

[JsonSerializable(typeof(GeneratedPerson))]
[JsonSerializable(typeof(GeneratedContainer))]
internal partial class TestYamlSerializerContext : YamlSerializerContext
{
}

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
}
