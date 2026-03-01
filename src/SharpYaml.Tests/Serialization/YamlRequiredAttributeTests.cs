using System.Text.Json.Serialization;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SharpYaml.Serialization;

namespace SharpYaml.Tests.Serialization;

[TestClass]
public sealed class YamlRequiredAttributeTests
{
    [TestMethod]
    public void Deserialize_WhenYamlRequiredMissing_ThrowsYamlException()
    {
        var ex = Assert.Throws<YamlException>(() => YamlSerializer.Deserialize<YamlRequiredModel>("b: 2\n"));
        StringAssert.Contains(ex.Message, "a");
    }

    [TestMethod]
    public void Deserialize_WhenJsonRequiredMissing_ThrowsYamlException()
    {
        var ex = Assert.Throws<YamlException>(() => YamlSerializer.Deserialize<JsonRequiredModel>("b: 2\n"));
        StringAssert.Contains(ex.Message, "a");
    }

    [TestMethod]
    public void Deserialize_WhenRequiredPresent_Succeeds()
    {
        var value = YamlSerializer.Deserialize<YamlRequiredModel>("A: 1\nB: 2\n")!;
        Assert.AreEqual(1, value.A);
        Assert.AreEqual(2, value.B);
    }

    private sealed class YamlRequiredModel
    {
        [YamlRequired]
        public int A { get; set; }

        public int B { get; set; }
    }

    private sealed class JsonRequiredModel
    {
        [JsonRequired]
        public int A { get; set; }

        public int B { get; set; }
    }
}
