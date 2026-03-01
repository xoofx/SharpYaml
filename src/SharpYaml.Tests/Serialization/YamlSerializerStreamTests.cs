using System.Collections.Generic;
using System.IO;
using System.Text;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace SharpYaml.Tests.Serialization;

[TestClass]
public sealed class YamlSerializerStreamTests
{
    [TestMethod]
    public void Serialize_Stream_ShouldWriteYamlAndLeaveStreamOpen()
    {
        using var stream = new MemoryStream();

        YamlSerializer.Serialize(stream, new Dictionary<string, int> { ["a"] = 1 });

        Assert.IsTrue(stream.CanRead);
        stream.Position = 0;
        using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true, leaveOpen: true);
        var yaml = reader.ReadToEnd();

        StringAssert.Contains(yaml, "a:");
        StringAssert.Contains(yaml, "1");
    }

    [TestMethod]
    public void Deserialize_Stream_ShouldReadYamlAndLeaveStreamOpen()
    {
        var data = Encoding.UTF8.GetBytes("a: 1\n");
        using var stream = new MemoryStream(data);

        var dict = YamlSerializer.Deserialize<Dictionary<string, int>>(stream);

        Assert.IsNotNull(dict);
        Assert.AreEqual(1, dict["a"]);
        Assert.IsTrue(stream.CanRead);
    }

    [TestMethod]
    public void SerializeAndDeserialize_Stream_WithContext()
    {
        var context = new TestYamlSerializerContext();
        var person = new GeneratedPerson { FirstName = "Bob", Age = 42 };

        using var stream = new MemoryStream();
        YamlSerializer.Serialize(stream, person, context);

        Assert.IsTrue(stream.CanRead);
        stream.Position = 0;

        var roundtripped = YamlSerializer.Deserialize<GeneratedPerson>(stream, context);

        Assert.IsNotNull(roundtripped);
        Assert.AreEqual("Bob", roundtripped.FirstName);
        Assert.AreEqual(42, roundtripped.Age);
    }
}

