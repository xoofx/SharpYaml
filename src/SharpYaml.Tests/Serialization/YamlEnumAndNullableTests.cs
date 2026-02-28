#nullable enable

using System;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace SharpYaml.Tests.Serialization;

[TestClass]
public sealed class YamlEnumAndNullableTests
{
    private enum Color
    {
        Red = 1,
        Green = 2,
    }

    [TestMethod]
    public void Enum_ReadsFromNameOrNumber()
    {
        Assert.AreEqual(Color.Green, YamlSerializer.Deserialize<Color>("green"));
        Assert.AreEqual(Color.Green, YamlSerializer.Deserialize<Color>("2"));
    }

    [TestMethod]
    public void Enum_InvalidValue_ThrowsYamlExceptionWithContext()
    {
        var options = new YamlSerializerOptions { SourceName = "colors.yaml" };
        var ex = Assert.Throws<YamlException>(() => YamlSerializer.Deserialize<Color>("unknown", options));

        Assert.AreEqual("colors.yaml", ex.SourceName);
        Assert.IsTrue(ex.Start.Index >= 0);
        StringAssert.Contains(ex.Message, "unknown");
    }

    [TestMethod]
    public void Nullable_Primitives_RoundTrip()
    {
        int? value = 123;
        var yaml = YamlSerializer.Serialize(value);
        Assert.AreEqual(123, YamlSerializer.Deserialize<int?>(yaml));

        int? nullValue = null;
        var nullYaml = YamlSerializer.Serialize(nullValue);
        Assert.IsNull(YamlSerializer.Deserialize<int?>(nullYaml));
    }

    [TestMethod]
    public void Nullable_Elements_RoundTripInSequence()
    {
        var yaml = "- 1\n- null\n- 3\n";
        var values = YamlSerializer.Deserialize<int?[]>(yaml);

        Assert.IsNotNull(values);
        CollectionAssert.AreEqual(new int?[] { 1, null, 3 }, values);
    }
}

