using System.Buffers;
using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace SharpYaml.Tests.Serialization;

[TestClass]
public sealed class YamlSerializerBufferWriterTests
{
    [TestMethod]
    public void Serialize_IBufferWriter_ShouldWriteYaml()
    {
        var writer = new ArrayBufferWriter<char>();

        YamlSerializer.Serialize(writer, new Dictionary<string, int> { ["a"] = 1 });

        var yaml = new string(writer.WrittenSpan);
        StringAssert.Contains(yaml, "a:");
        StringAssert.Contains(yaml, "1");
    }

    [TestMethod]
    public void Serialize_IBufferWriter_WithContext_ShouldWriteYaml()
    {
        var writer = new ArrayBufferWriter<char>();
        var context = new TestYamlSerializerContext();

        YamlSerializer.Serialize(writer, new GeneratedPerson { FirstName = "Bob", Age = 42 }, context);

        var yaml = new string(writer.WrittenSpan);
        StringAssert.Contains(yaml, "first_name");
        StringAssert.Contains(yaml, "Bob");
        StringAssert.Contains(yaml, "Age");
    }
}
