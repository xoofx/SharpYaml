using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SharpYaml.Serialization;

namespace SharpYaml.Tests.Serialization;

[TestClass]
public sealed class YamlWriterTests
{
    [TestMethod]
    public void Mapping_WithScalar_WritesOnSingleLine()
    {
        var options = new YamlSerializerOptions { WriteIndented = true, IndentSize = 2 };
        var writer = CreateWriter(options, out var buffer);

        writer.WriteStartMapping();
        writer.WritePropertyName("a");
        writer.WriteScalar("1");
        writer.WriteEndMapping();

        Assert.AreEqual("a: 1", buffer.ToString());
    }

    [TestMethod]
    public void Mapping_WithNestedMapping_WritesIndentedBlock()
    {
        var options = new YamlSerializerOptions { WriteIndented = true, IndentSize = 2 };
        var writer = CreateWriter(options, out var buffer);

        writer.WriteStartMapping();
        writer.WritePropertyName("parent");
        writer.WriteStartMapping();
        writer.WritePropertyName("child");
        writer.WriteScalar("x");
        writer.WriteEndMapping();
        writer.WriteEndMapping();

        Assert.AreEqual("parent:\n  child: x", buffer.ToString());
    }

    [TestMethod]
    public void Sequence_WithScalars_WritesDashLines()
    {
        var options = new YamlSerializerOptions { WriteIndented = true, IndentSize = 2 };
        var writer = CreateWriter(options, out var buffer);

        writer.WriteStartSequence();
        writer.WriteScalar("a");
        writer.WriteScalar("b");
        writer.WriteEndSequence();

        Assert.AreEqual("- a\n- b", buffer.ToString());
    }

    [TestMethod]
    public void EmptyContainers_AreWrittenInline()
    {
        var options = new YamlSerializerOptions { WriteIndented = true, IndentSize = 2 };
        var writer = CreateWriter(options, out var buffer);

        writer.WriteStartMapping();
        writer.WritePropertyName("emptyMap");
        writer.WriteStartMapping();
        writer.WriteEndMapping();
        writer.WritePropertyName("emptySeq");
        writer.WriteStartSequence();
        writer.WriteEndSequence();
        writer.WriteEndMapping();

        Assert.AreEqual("emptyMap: {}\nemptySeq: []", buffer.ToString());
    }

    [TestMethod]
    public void SequenceItem_EmptyMapping_WritesInline()
    {
        var options = new YamlSerializerOptions { WriteIndented = true, IndentSize = 2 };
        var writer = CreateWriter(options, out var buffer);

        writer.WriteStartSequence();
        writer.WriteStartMapping();
        writer.WriteEndMapping();
        writer.WriteEndSequence();

        Assert.AreEqual("- {}", buffer.ToString());
    }

    private static YamlWriter CreateWriter(YamlSerializerOptions options, out StringWriter buffer)
    {
        buffer = new StringWriter();
        return new YamlWriter(buffer, options);
    }
}

