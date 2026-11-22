using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SharpYaml.Schemas;
using SharpYaml.Events;

namespace SharpYaml.Tests.Serialization;

[TestClass]
public sealed class Yaml122SchemaTests
{
    [TestMethod]
    [DataRow("1_000")]
    [DataRow("1.2_3")]
    [DataRow("0b101")]
    [DataRow("0Xff")]
    [DataRow("-0x10")]
    [DataRow("+0o7")]
    [DataRow("tRuE")]
    [DataRow("nUlL")]
    [DataRow(".iNf")]
    [DataRow("+.nan")]
    public void CoreLeavesNonCoreSpellingsAsStrings(string text)
    {
        foreach (var useSchema in new[] { false, true })
        {
            Assert.AreEqual(text, YamlSerializer.Deserialize<object>(text, new YamlSerializerOptions { UseSchema = useSchema }));
        }
        Assert.AreEqual(SchemaBase.StrShortTag, CoreSchema.Instance.GetDefaultTag(new Scalar(text)));
    }

    [TestMethod]
    [DataRow("017", 17d)]
    [DataRow("+017", 17d)]
    [DataRow("0o17", 15d)]
    [DataRow("0x1F", 31d)]
    [DataRow(".5", .5d)]
    [DataRow("1.e+3", 1000d)]
    public void CoreNumericFormsAgreeAcrossResolutionPaths(string text, double expected)
    {
        foreach (var useSchema in new[] { false, true })
        {
            Assert.AreEqual(expected, Convert.ToDouble(YamlSerializer.Deserialize<object>(text, new YamlSerializerOptions { UseSchema = useSchema })));
        }
    }

    [TestMethod]
    [DataRow("0.12345678901234567890123")]
    [DataRow("1234567890.123456789")]
    [DataRow("79228162514264337593543950335")]
    public void DecimalKeepsItsPrecisionInBothPaths(string text)
    {
        var expected = decimal.Parse(text, System.Globalization.CultureInfo.InvariantCulture);
        foreach (var useSchema in new[] { false, true })
        {
            Assert.AreEqual(expected, YamlSerializer.Deserialize<decimal>(text, new YamlSerializerOptions { UseSchema = useSchema }));
        }
    }

    [TestMethod]
    [DataRow(YamlSchemaKind.Extended, "!!float 017", "15")]
    [DataRow(YamlSchemaKind.Extended, "!!float '017'", "15")]
    [DataRow(YamlSchemaKind.Core, "!!float 0o17", "15")]
    [DataRow(YamlSchemaKind.Core, "!!float 12345678901234567890123", "12345678901234567890123")]
    [DataRow(YamlSchemaKind.Core, "!!float 1234567890.123456789", "1234567890.123456789")]
    public void DecimalWithExplicitFloatTagKeepsTheValueOfTheSchema(YamlSchemaKind schema, string yaml, string expected)
    {
        var options = new YamlSerializerOptions { Schema = schema, UseSchema = true };
        Assert.AreEqual(decimal.Parse(expected, System.Globalization.CultureInfo.InvariantCulture), YamlSerializer.Deserialize<decimal>(yaml, options));
    }

    [TestMethod]
    [DataRow("! 12", "12")]
    [DataRow("!!str 12", "12")]
    [DataRow("!!int '12'", 12)]
    [DataRow("!!bool 'true'", true)]
    [DataRow("!!float 1", 1d)]
    public void ExplicitTagsAreHonoredInBothPaths(string yaml, object expected)
    {
        foreach (var useSchema in new[] { false, true })
        {
            var value = YamlSerializer.Deserialize<object>(yaml, new YamlSerializerOptions { UseSchema = useSchema });
            if (expected is int) Assert.AreEqual(Convert.ToInt64(expected), Convert.ToInt64(value));
            else Assert.AreEqual(expected, value);
        }
    }
}
