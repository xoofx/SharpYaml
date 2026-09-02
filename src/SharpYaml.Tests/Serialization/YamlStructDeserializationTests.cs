using Microsoft.VisualStudio.TestTools.UnitTesting;
using SharpYaml.Serialization;

namespace SharpYaml.Tests.Serialization;

[TestClass]
public sealed class YamlStructDeserializationTests
{
    private const string Yaml = "A: 5\nB: hello\n";

    [TestMethod]
    public void Reflection_DeserializesPlainStruct()
    {
        var value = YamlSerializer.Deserialize<PlainStruct>(Yaml);

        Assert.AreEqual(5, value.A);
        Assert.AreEqual("hello", value.B);
    }

    [TestMethod]
    public void Reflection_DeserializesRecordStruct()
    {
        var value = YamlSerializer.Deserialize<RecordStruct>(Yaml);

        Assert.AreEqual(new RecordStruct(5, "hello"), value);
    }

    [TestMethod]
    public void SourceGeneration_DeserializesPlainStruct()
    {
        var value = YamlSerializer.Deserialize(Yaml, StructYamlSerializerContext.Default.PlainStruct);

        Assert.AreEqual(5, value.A);
        Assert.AreEqual("hello", value.B);
    }

    [TestMethod]
    public void SourceGeneration_DeserializesRecordStruct()
    {
        var value = YamlSerializer.Deserialize(Yaml, StructYamlSerializerContext.Default.RecordStruct);

        Assert.AreEqual(new RecordStruct(5, "hello"), value);
    }
}

internal struct PlainStruct
{
    public int A { get; set; }

    public string? B { get; set; }
}

internal readonly record struct RecordStruct(int A, string B);

[YamlSerializable(typeof(PlainStruct))]
[YamlSerializable(typeof(RecordStruct))]
internal partial class StructYamlSerializerContext : YamlSerializerContext
{
}
