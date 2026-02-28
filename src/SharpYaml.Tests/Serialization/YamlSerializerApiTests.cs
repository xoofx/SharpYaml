#nullable enable

using System;
using System.Text.Json.Serialization;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SharpYaml.Serialization;

namespace SharpYaml.Tests.Serialization;

[TestClass]
public class YamlSerializerApiTests
{
    private sealed class Person
    {
        public string FirstName { get; set; } = string.Empty;

        public int Age { get; set; }
    }

    private sealed class OrderingModel
    {
        public string Zeta { get; set; } = string.Empty;

        public string Alpha { get; set; } = string.Empty;
    }

    private sealed class StringTypeInfo : YamlTypeInfo<string>
    {
        public StringTypeInfo(YamlSerializerOptions options) : base(options)
        {
        }

        public override string Serialize(string value)
        {
            return $"value: {value}";
        }

        public override string Deserialize(string yaml)
        {
            var parts = yaml.Split(':', 2);
            return parts.Length == 2 ? parts[1].Trim() : string.Empty;
        }
    }

    private sealed class StringTypeInfoResolver : IYamlTypeInfoResolver
    {
        public StringTypeInfoResolver(YamlTypeInfo typeInfo)
        {
            TypeInfo = typeInfo;
        }

        public YamlTypeInfo TypeInfo { get; }

        public YamlTypeInfo? GetTypeInfo(Type type, YamlSerializerOptions options)
        {
            return type == typeof(string) ? TypeInfo : null;
        }
    }

    private sealed class JsonAnnotatedPerson
    {
        [JsonPropertyOrder(-10)]
        public int Age { get; set; }

        [JsonPropertyName("first_name")]
        public string FirstName { get; set; } = string.Empty;

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? NickName { get; set; }

        [JsonInclude]
        public string Secret { get; private set; } = string.Empty;

        public void SetSecret(string secret)
        {
            Secret = secret;
        }
    }

    private sealed class YamlAndJsonNamedModel
    {
        [JsonPropertyName("json_name")]
        [YamlPropertyName("yaml_name")]
        [JsonPropertyOrder(100)]
        [YamlPropertyOrder(-100)]
        public string Name { get; set; } = string.Empty;

        public int Rank { get; set; }
    }

    [TestMethod]
    public void SerializeAndDeserializeRoundTrip()
    {
        var value = new Person { FirstName = "Ada", Age = 37 };
        var yaml = YamlSerializer.Serialize(value);
        var roundTrip = YamlSerializer.Deserialize<Person>(yaml);

        Assert.IsNotNull(roundTrip);
        Assert.AreEqual("Ada", roundTrip.FirstName);
        Assert.AreEqual(37, roundTrip.Age);
    }

    [TestMethod]
    public void MappingOrderDefaultsToDeclaration()
    {
        var yaml = YamlSerializer.Serialize(new OrderingModel
        {
            Zeta = "z",
            Alpha = "a",
        });

        var zetaIndex = yaml.IndexOf("Zeta:", StringComparison.Ordinal);
        var alphaIndex = yaml.IndexOf("Alpha:", StringComparison.Ordinal);
        Assert.IsTrue(zetaIndex >= 0);
        Assert.IsTrue(alphaIndex > zetaIndex);
    }

    [TestMethod]
    public void MappingOrderCanBeSorted()
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

        var zetaIndex = yaml.IndexOf("Zeta:", StringComparison.Ordinal);
        var alphaIndex = yaml.IndexOf("Alpha:", StringComparison.Ordinal);
        Assert.IsTrue(alphaIndex >= 0);
        Assert.IsTrue(zetaIndex > alphaIndex);
    }

    [TestMethod]
    public void SerializeWithCamelCasePolicy()
    {
        var options = new YamlSerializerOptions { PropertyNamingPolicy = YamlNamingPolicy.CamelCase };
        var yaml = YamlSerializer.Serialize(new Person { FirstName = "Ada", Age = 37 }, options);

        StringAssert.Contains(yaml, "firstName");
        StringAssert.Contains(yaml, "age");
    }

    [TestMethod]
    public void DeserializeFromReadOnlySpan()
    {
        ReadOnlySpan<char> yaml = "FirstName: Ada\nAge: 37";
        var result = YamlSerializer.Deserialize<Person>(yaml);

        Assert.IsNotNull(result);
        Assert.AreEqual("Ada", result.FirstName);
        Assert.AreEqual(37, result.Age);
    }

    [TestMethod]
    public void SerializeWithExplicitTypeInfo()
    {
        var typeInfo = new StringTypeInfo(new YamlSerializerOptions());
        var yaml = YamlSerializer.Serialize("hello", typeInfo);
        var value = YamlSerializer.Deserialize(yaml, typeInfo);

        Assert.AreEqual("value: hello", yaml);
        Assert.AreEqual("hello", value);
    }

    [TestMethod]
    public void SerializeWithResolverTypeInfo()
    {
        var typeInfo = new StringTypeInfo(new YamlSerializerOptions());
        var options = new YamlSerializerOptions
        {
            TypeInfoResolver = new StringTypeInfoResolver(typeInfo),
        };

        var yaml = YamlSerializer.Serialize("hello", typeof(string), options);
        var value = YamlSerializer.Deserialize(yaml, typeof(string), options);

        Assert.AreEqual("value: hello", yaml);
        Assert.AreEqual("hello", value);
    }

    [TestMethod]
    public void JsonAttributesAreRespectedByReflectionSerializer()
    {
        var person = new JsonAnnotatedPerson
        {
            Age = 37,
            FirstName = "Ada",
            NickName = null,
        };
        person.SetSecret("s3cr3t");

        var yaml = YamlSerializer.Serialize(person);
        var ageIndex = yaml.IndexOf("Age:", StringComparison.Ordinal);
        var firstNameIndex = yaml.IndexOf("first_name:", StringComparison.Ordinal);

        Assert.IsTrue(ageIndex >= 0);
        Assert.IsTrue(firstNameIndex > ageIndex);
        StringAssert.Contains(yaml, "first_name: Ada");
        StringAssert.Contains(yaml, "Secret: s3cr3t");
        Assert.IsFalse(yaml.Contains("NickName:", StringComparison.Ordinal));

        var roundTrip = YamlSerializer.Deserialize<JsonAnnotatedPerson>(
            "Age: 37\nfirst_name: Ada\nSecret: from-yaml");

        Assert.IsNotNull(roundTrip);
        Assert.AreEqual(37, roundTrip.Age);
        Assert.AreEqual("Ada", roundTrip.FirstName);
        Assert.AreEqual("from-yaml", roundTrip.Secret);
    }

    [TestMethod]
    public void YamlSpecificAttributesOverrideJsonAttributes()
    {
        var yaml = YamlSerializer.Serialize(new YamlAndJsonNamedModel
        {
            Name = "value",
            Rank = 7,
        });
        var yamlNameIndex = yaml.IndexOf("yaml_name:", StringComparison.Ordinal);
        var rankIndex = yaml.IndexOf("Rank:", StringComparison.Ordinal);

        Assert.IsTrue(yamlNameIndex >= 0);
        Assert.IsTrue(rankIndex > yamlNameIndex);
        Assert.IsFalse(yaml.Contains("json_name:", StringComparison.Ordinal));

        var roundTrip = YamlSerializer.Deserialize<YamlAndJsonNamedModel>("yaml_name: value\nRank: 7");
        Assert.IsNotNull(roundTrip);
        Assert.AreEqual("value", roundTrip.Name);
        Assert.AreEqual(7, roundTrip.Rank);
    }
}
