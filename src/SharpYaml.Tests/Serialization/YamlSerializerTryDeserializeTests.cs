using System.Collections.Generic;
using System.IO;
using System.Text;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace SharpYaml.Tests.Serialization;

[TestClass]
public sealed class YamlSerializerTryDeserializeTests
{
    [TestMethod]
    public void TryDeserialize_InvalidYaml_ReturnsFalse()
    {
        var ok = YamlSerializer.TryDeserialize<Dictionary<string, int>>("a: [", out var value);

        Assert.IsFalse(ok);
        Assert.IsNull(value);
    }

    [TestMethod]
    public void TryDeserialize_InvalidYaml_WithContext_ReturnsFalse()
    {
        var context = new TestYamlSerializerContext();

        var ok = YamlSerializer.TryDeserialize<GeneratedPerson>("a: [", context, out var value);

        Assert.IsFalse(ok);
        Assert.IsNull(value);
    }

    [TestMethod]
    public void TryDeserialize_InvalidYaml_FromTextReader_ReturnsFalse()
    {
        using var reader = new StringReader("a: [");

        var ok = YamlSerializer.TryDeserialize<Dictionary<string, int>>(reader, out var value);

        Assert.IsFalse(ok);
        Assert.IsNull(value);
    }

    [TestMethod]
    public void TryDeserialize_ValidYaml_ReturnsTrue()
    {
        var ok = YamlSerializer.TryDeserialize<Dictionary<string, int>>("a: 1\n", out var value);

        Assert.IsTrue(ok);
        Assert.IsNotNull(value);
        Assert.AreEqual(1, value["a"]);
    }

    [TestMethod]
    public void TryDeserialize_ValidYaml_FromStream_ReturnsTrue()
    {
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes("a: 1\n"));

        var ok = YamlSerializer.TryDeserialize<Dictionary<string, int>>(stream, out var value);

        Assert.IsTrue(ok);
        Assert.IsNotNull(value);
        Assert.AreEqual(1, value["a"]);
    }
}
