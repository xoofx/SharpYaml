#nullable enable

using System.Text.Json.Serialization;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SharpYaml.Serialization;

namespace SharpYaml.Tests.Serialization;

internal abstract class GeneratedInheritedJsonNamedBase
{
    [JsonPropertyName("base_value")]
    public string? BaseValue { get; init; }
}

internal sealed class GeneratedInheritedJsonNamedDerived : GeneratedInheritedJsonNamedBase
{
    [JsonPropertyName("derived_value")]
    public string? DerivedValue { get; init; }
}

[YamlSerializable(typeof(GeneratedInheritedJsonNamedDerived))]
internal partial class InheritedMemberYamlSerializerContext : YamlSerializerContext
{
}

[TestClass]
public class YamlInheritedMemberSourceGenerationTests
{
    [TestMethod]
    public void ReflectionDeserializerIncludesJsonNamedBaseMembers()
    {
        var yaml = "base_value: base\nderived_value: derived\n";

        var result = YamlSerializer.Deserialize<GeneratedInheritedJsonNamedDerived>(yaml);

        Assert.IsNotNull(result);
        Assert.AreEqual("base", result.BaseValue);
        Assert.AreEqual("derived", result.DerivedValue);
    }

    [TestMethod]
    public void ReflectionSerializerIncludesJsonNamedBaseMembers()
    {
        var yaml = YamlSerializer.Serialize(new GeneratedInheritedJsonNamedDerived
        {
            BaseValue = "base",
            DerivedValue = "derived",
        });

        StringAssert.Contains(yaml, "base_value: base");
        StringAssert.Contains(yaml, "derived_value: derived");
    }

    [TestMethod]
    public void SourceGeneratedDeserializerIncludesJsonNamedBaseMembers()
    {
        var yaml = "base_value: base\nderived_value: derived\n";
        var context = InheritedMemberYamlSerializerContext.Default;

        var result = YamlSerializer.Deserialize(yaml, context.GeneratedInheritedJsonNamedDerived);

        Assert.IsNotNull(result);
        Assert.AreEqual("base", result.BaseValue);
        Assert.AreEqual("derived", result.DerivedValue);
    }

    [TestMethod]
    public void SourceGeneratedSerializerIncludesJsonNamedBaseMembers()
    {
        var context = InheritedMemberYamlSerializerContext.Default;
        var yaml = YamlSerializer.Serialize(new GeneratedInheritedJsonNamedDerived
        {
            BaseValue = "base",
            DerivedValue = "derived",
        }, context.GeneratedInheritedJsonNamedDerived);

        StringAssert.Contains(yaml, "base_value: base");
        StringAssert.Contains(yaml, "derived_value: derived");
    }
}
