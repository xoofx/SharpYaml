#nullable enable

using System.Text.Json.Serialization;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SharpYaml.Serialization;

namespace SharpYaml.Tests.Serialization;

[TestClass]
public class YamlSerializerGoldenTests
{
    private sealed class OrderingModel
    {
        public string Zeta { get; set; } = string.Empty;

        public string Alpha { get; set; } = string.Empty;
    }

    private sealed class JsonAnnotatedPerson
    {
        [JsonPropertyOrder(-10)]
        public int Age { get; set; }

        [JsonPropertyName("first_name")]
        public string FirstName { get; set; } = string.Empty;

        [JsonInclude]
        public string Secret { get; private set; } = string.Empty;

        public void SetSecret(string value)
        {
            Secret = value;
        }
    }

    [TestMethod]
    public void DeclarationOrderMatchesGoldenFile()
    {
        var yaml = YamlSerializer.Serialize(
            new OrderingModel
            {
                Zeta = "z",
                Alpha = "a",
            });

        GoldenFileAssert.AreEqual("v3/ordered_declaration.yaml", yaml);
    }

    [TestMethod]
    public void SortedOrderMatchesGoldenFile()
    {
        var yaml = YamlSerializer.Serialize(
            new OrderingModel
            {
                Zeta = "z",
                Alpha = "a",
            },
            new YamlSerializerOptions
            {
                MappingOrder = YamlMappingOrderPolicy.Sorted,
            });

        GoldenFileAssert.AreEqual("v3/ordered_sorted.yaml", yaml);
    }

    [TestMethod]
    public void JsonAttributesProjectionMatchesGoldenFile()
    {
        var person = new JsonAnnotatedPerson
        {
            Age = 37,
            FirstName = "Ada",
        };
        person.SetSecret("s3cr3t");

        var yaml = YamlSerializer.Serialize(person);
        GoldenFileAssert.AreEqual("v3/json_attributes.yaml", yaml);
    }
}
