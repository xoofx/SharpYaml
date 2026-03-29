using System.Text.Json.Serialization;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SharpYaml.Serialization;

namespace SharpYaml.Tests.Serialization;

/// <summary>
/// Tests for C# <c>required</c> keyword support in both reflection and source-generated modes.
/// Verifies that types with <c>required</c> members compile, deserialize, serialize, and validate correctly.
/// </summary>
[TestClass]
public sealed class CSharpRequiredKeywordTests
{
    // ─── Reflection mode tests ──────────────────────────────────────

    [TestMethod]
    public void Reflection_RequiredSetProperty_DeserializesCorrectly()
    {
        var result = YamlSerializer.Deserialize<CSharpRequiredSetModel>("Name: Alice\nValue: 42\n")!;
        Assert.AreEqual("Alice", result.Name);
        Assert.AreEqual(42, result.Value);
    }

    [TestMethod]
    public void Reflection_RequiredSetProperty_SerializesCorrectly()
    {
        var yaml = YamlSerializer.Serialize(new CSharpRequiredSetModel { Name = "Bob", Value = 7 });
        StringAssert.Contains(yaml, "Name: Bob");
        StringAssert.Contains(yaml, "Value: 7");
    }

    [TestMethod]
    public void Reflection_RequiredSetProperty_MissingRequired_Throws()
    {
        var ex = Assert.Throws<YamlException>(() => YamlSerializer.Deserialize<CSharpRequiredSetModel>("Value: 42\n"));
        StringAssert.Contains(ex.Message, "Name");
    }

    [TestMethod]
    public void Reflection_RequiredInitProperty_DeserializesCorrectly()
    {
        var result = YamlSerializer.Deserialize<CSharpRequiredInitModel>("Id: hello\nScore: 99\n")!;
        Assert.AreEqual("hello", result.Id);
        Assert.AreEqual(99, result.Score);
    }

    [TestMethod]
    public void Reflection_MixedRequiredModel_DeserializesCorrectly()
    {
        var result = YamlSerializer.Deserialize<CSharpMixedRequiredModel>("Name: Ada\nLabel: test\nId: abc\nOptional: opt\n")!;
        Assert.AreEqual("Ada", result.Name);
        Assert.AreEqual("test", result.Label);
        Assert.AreEqual("abc", result.Id);
        Assert.AreEqual("opt", result.Optional);
    }

    [TestMethod]
    public void Reflection_MixedRequiredModel_MissingCSharpRequired_Throws()
    {
        var ex = Assert.Throws<YamlException>(() =>
            YamlSerializer.Deserialize<CSharpMixedRequiredModel>("Label: test\nId: abc\n"));
        StringAssert.Contains(ex.Message, "Name");
    }

    [TestMethod]
    public void Reflection_MixedRequiredModel_MissingAttributeRequired_Throws()
    {
        var ex = Assert.Throws<YamlException>(() =>
            YamlSerializer.Deserialize<CSharpMixedRequiredModel>("Name: Ada\nId: abc\n"));
        StringAssert.Contains(ex.Message, "Label");
    }

    [TestMethod]
    public void Reflection_RequiredWithOptional_OptionalCanBeOmitted()
    {
        var result = YamlSerializer.Deserialize<CSharpRequiredSetModel>("Name: Alice\nValue: 1\n")!;
        Assert.AreEqual("Alice", result.Name);
        Assert.AreEqual(1, result.Value);
        Assert.IsNull(result.Optional);
    }

    [TestMethod]
    public void Reflection_RequiredRecord_DeserializesCorrectly()
    {
        var result = YamlSerializer.Deserialize<CSharpRequiredRecord>("Host: db-server\nPort: 5432\n")!;
        Assert.AreEqual("db-server", result.Host);
        Assert.AreEqual(5432, result.Port);
    }

    [TestMethod]
    public void Reflection_RequiredRecord_MissingRequired_Throws()
    {
        var ex = Assert.Throws<YamlException>(() =>
            YamlSerializer.Deserialize<CSharpRequiredRecord>("Port: 5432\n"));
        StringAssert.Contains(ex.Message, "Host");
    }

    [TestMethod]
    public void Reflection_RequiredValueType_DeserializesCorrectly()
    {
        var result = YamlSerializer.Deserialize<CSharpRequiredValueTypeModel>("Count: 10\nFlag: true\n")!;
        Assert.AreEqual(10, result.Count);
        Assert.AreEqual(true, result.Flag);
    }

    [TestMethod]
    public void Reflection_RequiredValueType_MissingRequired_Throws()
    {
        var ex = Assert.Throws<YamlException>(() =>
            YamlSerializer.Deserialize<CSharpRequiredValueTypeModel>("Flag: true\n"));
        StringAssert.Contains(ex.Message, "Count");
    }

    // ─── Source generation mode tests ───────────────────────────────

    [TestMethod]
    public void SourceGen_RequiredSetProperty_DeserializesCorrectly()
    {
        var context = CSharpRequiredKeywordTestContext.Default;
        var result = YamlSerializer.Deserialize("Name: Alice\nValue: 42\n", context.CSharpRequiredSetModel)!;
        Assert.AreEqual("Alice", result.Name);
        Assert.AreEqual(42, result.Value);
    }

    [TestMethod]
    public void SourceGen_RequiredSetProperty_SerializesCorrectly()
    {
        var context = CSharpRequiredKeywordTestContext.Default;
        var yaml = YamlSerializer.Serialize(new CSharpRequiredSetModel { Name = "Bob", Value = 7 }, context.CSharpRequiredSetModel);
        StringAssert.Contains(yaml, "Name: Bob");
        StringAssert.Contains(yaml, "Value: 7");
    }

    [TestMethod]
    public void SourceGen_RequiredSetProperty_MissingRequired_Throws()
    {
        var context = CSharpRequiredKeywordTestContext.Default;
        var ex = Assert.Throws<YamlException>(() =>
            YamlSerializer.Deserialize("Value: 42\n", context.CSharpRequiredSetModel));
        StringAssert.Contains(ex.Message, "Name");
    }

    [TestMethod]
    public void SourceGen_RequiredInitProperty_DeserializesCorrectly()
    {
        var context = CSharpRequiredKeywordTestContext.Default;
        var result = YamlSerializer.Deserialize("Id: hello\nScore: 99\n", context.CSharpRequiredInitModel)!;
        Assert.AreEqual("hello", result.Id);
        Assert.AreEqual(99, result.Score);
    }

    [TestMethod]
    public void SourceGen_RequiredInitProperty_MissingRequired_Throws()
    {
        var context = CSharpRequiredKeywordTestContext.Default;
        var ex = Assert.Throws<YamlException>(() =>
            YamlSerializer.Deserialize("Score: 99\n", context.CSharpRequiredInitModel));
        StringAssert.Contains(ex.Message, "Id");
    }

    [TestMethod]
    public void SourceGen_MixedRequiredModel_DeserializesCorrectly()
    {
        var context = CSharpRequiredKeywordTestContext.Default;
        var result = YamlSerializer.Deserialize("Name: Ada\nLabel: test\nId: abc\nOptional: opt\n", context.CSharpMixedRequiredModel)!;
        Assert.AreEqual("Ada", result.Name);
        Assert.AreEqual("test", result.Label);
        Assert.AreEqual("abc", result.Id);
        Assert.AreEqual("opt", result.Optional);
    }

    [TestMethod]
    public void SourceGen_MixedRequiredModel_MissingCSharpRequired_Throws()
    {
        var context = CSharpRequiredKeywordTestContext.Default;
        var ex = Assert.Throws<YamlException>(() =>
            YamlSerializer.Deserialize("Label: test\nId: abc\n", context.CSharpMixedRequiredModel));
        StringAssert.Contains(ex.Message, "Name");
    }

    [TestMethod]
    public void SourceGen_MixedRequiredModel_MissingAttributeRequired_Throws()
    {
        var context = CSharpRequiredKeywordTestContext.Default;
        var ex = Assert.Throws<YamlException>(() =>
            YamlSerializer.Deserialize("Name: Ada\nId: abc\n", context.CSharpMixedRequiredModel));
        StringAssert.Contains(ex.Message, "Label");
    }

    [TestMethod]
    public void SourceGen_MixedRequiredModel_MissingMultiple_ThrowsWithAllNames()
    {
        var context = CSharpRequiredKeywordTestContext.Default;
        var ex = Assert.Throws<YamlException>(() =>
            YamlSerializer.Deserialize("Id: abc\n", context.CSharpMixedRequiredModel));
        StringAssert.Contains(ex.Message, "Name");
        StringAssert.Contains(ex.Message, "Label");
    }

    [TestMethod]
    public void SourceGen_RequiredWithOptional_OptionalCanBeOmitted()
    {
        var context = CSharpRequiredKeywordTestContext.Default;
        var result = YamlSerializer.Deserialize("Name: Alice\nValue: 1\n", context.CSharpRequiredSetModel)!;
        Assert.AreEqual("Alice", result.Name);
        Assert.AreEqual(1, result.Value);
        Assert.IsNull(result.Optional);
    }

    [TestMethod]
    public void SourceGen_RequiredRecord_DeserializesCorrectly()
    {
        var context = CSharpRequiredKeywordTestContext.Default;
        var result = YamlSerializer.Deserialize("Host: db-server\nPort: 5432\n", context.CSharpRequiredRecord)!;
        Assert.AreEqual("db-server", result.Host);
        Assert.AreEqual(5432, result.Port);
    }

    [TestMethod]
    public void SourceGen_RequiredRecord_SerializesCorrectly()
    {
        var context = CSharpRequiredKeywordTestContext.Default;
        var yaml = YamlSerializer.Serialize(new CSharpRequiredRecord { Host = "localhost", Port = 80 }, context.CSharpRequiredRecord);
        StringAssert.Contains(yaml, "Host: localhost");
        StringAssert.Contains(yaml, "Port: 80");
    }

    [TestMethod]
    public void SourceGen_RequiredRecord_MissingRequired_Throws()
    {
        var context = CSharpRequiredKeywordTestContext.Default;
        var ex = Assert.Throws<YamlException>(() =>
            YamlSerializer.Deserialize("Port: 5432\n", context.CSharpRequiredRecord));
        StringAssert.Contains(ex.Message, "Host");
    }

    [TestMethod]
    public void SourceGen_RequiredValueType_DeserializesCorrectly()
    {
        var context = CSharpRequiredKeywordTestContext.Default;
        var result = YamlSerializer.Deserialize("Count: 10\nFlag: true\n", context.CSharpRequiredValueTypeModel)!;
        Assert.AreEqual(10, result.Count);
        Assert.AreEqual(true, result.Flag);
    }

    [TestMethod]
    public void SourceGen_RequiredValueType_MissingRequired_Throws()
    {
        var context = CSharpRequiredKeywordTestContext.Default;
        var ex = Assert.Throws<YamlException>(() =>
            YamlSerializer.Deserialize("Flag: true\n", context.CSharpRequiredValueTypeModel));
        StringAssert.Contains(ex.Message, "Count");
    }

    [TestMethod]
    public void SourceGen_RequiredWithNamingPolicy_DeserializesCorrectly()
    {
        var context = CSharpRequiredKeywordCamelCaseContext.Default;
        var result = YamlSerializer.Deserialize("firstName: Ada\nlastName: Lovelace\n", context.CSharpRequiredNamingPolicyModel)!;
        Assert.AreEqual("Ada", result.FirstName);
        Assert.AreEqual("Lovelace", result.LastName);
    }

    [TestMethod]
    public void SourceGen_RequiredWithNamingPolicy_MissingRequired_ThrowsWithYamlName()
    {
        var context = CSharpRequiredKeywordCamelCaseContext.Default;
        var ex = Assert.Throws<YamlException>(() =>
            YamlSerializer.Deserialize("lastName: Lovelace\n", context.CSharpRequiredNamingPolicyModel));
        StringAssert.Contains(ex.Message, "firstName");
    }

    [TestMethod]
    public void SourceGen_RequiredWithCustomOptions_DeserializesCorrectly()
    {
        var context = new CSharpRequiredKeywordTestContext(new YamlSerializerOptions { PropertyNameCaseInsensitive = true });
        var result = YamlSerializer.Deserialize("name: Alice\nvalue: 42\n", context.CSharpRequiredSetModel)!;
        Assert.AreEqual("Alice", result.Name);
        Assert.AreEqual(42, result.Value);
    }

    [TestMethod]
    public void SourceGen_RequiredInheritance_DeserializesCorrectly()
    {
        var context = CSharpRequiredKeywordTestContext.Default;
        var result = YamlSerializer.Deserialize("Host: db-server\nPort: 5432\nTimeout: 30\n", context.CSharpRequiredDerivedModel)!;
        Assert.AreEqual("db-server", result.Host);
        Assert.AreEqual(5432, result.Port);
        Assert.AreEqual(30, result.Timeout);
    }

    [TestMethod]
    public void SourceGen_RequiredInheritance_MissingBaseRequired_Throws()
    {
        var context = CSharpRequiredKeywordTestContext.Default;
        var ex = Assert.Throws<YamlException>(() =>
            YamlSerializer.Deserialize("Port: 5432\nTimeout: 30\n", context.CSharpRequiredDerivedModel));
        StringAssert.Contains(ex.Message, "Host");
    }

    [TestMethod]
    public void SourceGen_RequiredInheritance_MissingDerivedRequired_Throws()
    {
        var context = CSharpRequiredKeywordTestContext.Default;
        var ex = Assert.Throws<YamlException>(() =>
            YamlSerializer.Deserialize("Host: db-server\nTimeout: 30\n", context.CSharpRequiredDerivedModel));
        StringAssert.Contains(ex.Message, "Port");
    }

    [TestMethod]
    public void SourceGen_RequiredOnlyModel_DeserializesCorrectly()
    {
        var context = CSharpRequiredKeywordTestContext.Default;
        var result = YamlSerializer.Deserialize("Tag: test\n", context.CSharpRequiredOnlyModel)!;
        Assert.AreEqual("test", result.Tag);
    }

    [TestMethod]
    public void SourceGen_RequiredOnlyModel_MissingRequired_Throws()
    {
        var context = CSharpRequiredKeywordTestContext.Default;
        var ex = Assert.Throws<YamlException>(() =>
            YamlSerializer.Deserialize("{}\n", context.CSharpRequiredOnlyModel));
        StringAssert.Contains(ex.Message, "Tag");
    }
}

// ─── Model types (at namespace level for source generator) ──────

public sealed class CSharpRequiredSetModel
{
    public required string Name { get; set; }
    public required int Value { get; set; }
    public string? Optional { get; set; }
}

public sealed class CSharpRequiredInitModel
{
    public required string Id { get; init; }
    public int Score { get; set; }
}

public sealed class CSharpMixedRequiredModel
{
    public required string Name { get; set; }

    [YamlRequired]
    public string Label { get; set; } = "";

    public required string Id { get; init; }

    public string? Optional { get; set; }
}

public record CSharpRequiredRecord
{
    public required string Host { get; set; }
    public required int Port { get; set; }
}

public sealed class CSharpRequiredValueTypeModel
{
    public required int Count { get; set; }
    public required bool Flag { get; set; }
}

public sealed class CSharpRequiredNamingPolicyModel
{
    public required string FirstName { get; set; }
    public required string LastName { get; set; }
}

public class CSharpRequiredBaseModel
{
    public required string Host { get; set; }
}

public sealed class CSharpRequiredDerivedModel : CSharpRequiredBaseModel
{
    public required int Port { get; set; }
    public int Timeout { get; set; }
}

public sealed class CSharpRequiredOnlyModel
{
    public required string Tag { get; set; }
}

// ─── Source generation contexts ─────────────────────────────────

[YamlSerializable(typeof(CSharpRequiredSetModel))]
[YamlSerializable(typeof(CSharpRequiredInitModel))]
[YamlSerializable(typeof(CSharpMixedRequiredModel))]
[YamlSerializable(typeof(CSharpRequiredRecord))]
[YamlSerializable(typeof(CSharpRequiredValueTypeModel))]
[YamlSerializable(typeof(CSharpRequiredDerivedModel))]
[YamlSerializable(typeof(CSharpRequiredOnlyModel))]
internal partial class CSharpRequiredKeywordTestContext : YamlSerializerContext
{
    public CSharpRequiredKeywordTestContext()
    {
    }

    public CSharpRequiredKeywordTestContext(YamlSerializerOptions options)
        : base(options)
    {
    }
}

[YamlSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[YamlSerializable(typeof(CSharpRequiredNamingPolicyModel))]
internal partial class CSharpRequiredKeywordCamelCaseContext : YamlSerializerContext
{
}
