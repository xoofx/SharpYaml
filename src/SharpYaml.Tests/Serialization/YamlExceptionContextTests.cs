using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace SharpYaml.Tests.Serialization;

[TestClass]
public sealed class YamlExceptionContextTests
{
    [TestMethod]
    public void ReflectionDeserializationErrorsIncludeSourceNameAndLocation()
    {
        var options = new YamlSerializerOptions
        {
            SourceName = "config.yaml",
        };

        var exception = Assert.Throws<YamlException>(() => YamlSerializer.Deserialize<int>("a: b", options));
        Assert.AreEqual("config.yaml", exception.SourceName);
        StringAssert.Contains(exception.Message, "Lin:");
        StringAssert.Contains(exception.Message, "Col:");
    }
}
