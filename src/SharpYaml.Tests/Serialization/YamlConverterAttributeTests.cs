using System;
using System.Globalization;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SharpYaml.Serialization;

namespace SharpYaml.Tests.Serialization;

[TestClass]
public sealed class YamlConverterAttributeTests
{
    [TestMethod]
    public void Deserialize_UsesPropertyLevelConverter()
    {
        var value = YamlSerializer.Deserialize<PropertyLevelModel>("A: 1\n")!;
        Assert.AreEqual(2, value.A);
    }

    [TestMethod]
    public void Serialize_UsesPropertyLevelConverter()
    {
        var yaml = YamlSerializer.Serialize(new PropertyLevelModel { A = 1 });
        StringAssert.Contains(yaml, "A: 2");
    }

    [TestMethod]
    public void Serialize_UsesTypeLevelConverter()
    {
        var yaml = YamlSerializer.Serialize(new TypeLevelContainer { Value = new CustomScalar { Text = "hello" } });

        StringAssert.Contains(yaml, "Value: hello");
        Assert.IsFalse(yaml.Contains("Text:", StringComparison.Ordinal));
    }

    [TestMethod]
    public void Deserialize_UsesTypeLevelConverter()
    {
        var value = YamlSerializer.Deserialize<TypeLevelContainer>("Value: hello\n")!;
        Assert.IsNotNull(value.Value);
        Assert.AreEqual("hello", value.Value!.Text);
    }

    private sealed class PropertyLevelModel
    {
        [YamlConverter(typeof(IncrementIntConverter))]
        public int A { get; set; }
    }

    private sealed class IncrementIntConverter : YamlConverter<int>
    {
        public override int Read(YamlReader reader)
        {
            var scalar = reader.GetScalarValue();
            reader.Read();
            return int.Parse(scalar, CultureInfo.InvariantCulture) + 1;
        }

        public override void Write(YamlWriter writer, int value)
        {
            writer.WriteScalar((value + 1).ToString(CultureInfo.InvariantCulture));
        }
    }

    private sealed class TypeLevelContainer
    {
        public CustomScalar? Value { get; set; }
    }

    [YamlConverter(typeof(CustomScalarConverter))]
    private sealed class CustomScalar
    {
        public string? Text { get; set; }
    }

    private sealed class CustomScalarConverter : YamlConverter<CustomScalar?>
    {
        public override CustomScalar? Read(YamlReader reader)
        {
            if (reader.TokenType == YamlTokenType.Scalar && YamlScalar.IsNull(reader.ScalarValue.AsSpan()))
            {
                reader.Read();
                return null;
            }

            var scalar = reader.GetScalarValue();
            reader.Read();
            return new CustomScalar { Text = scalar };
        }

        public override void Write(YamlWriter writer, CustomScalar? value)
        {
            if (value is null)
            {
                writer.WriteNullValue();
                return;
            }

            writer.WriteScalar(value.Text);
        }
    }
}
