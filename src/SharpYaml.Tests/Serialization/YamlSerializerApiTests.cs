using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace SharpYaml.Tests.Serialization;

[TestClass]
public class YamlSerializerApiTests
{
    private sealed class Person
    {
        public string FirstName { get; set; } = string.Empty;

        public int Age { get; set; }
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

        public YamlTypeInfo GetTypeInfo(Type type, YamlSerializerOptions options)
        {
            return type == typeof(string) ? TypeInfo : null;
        }
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
}
