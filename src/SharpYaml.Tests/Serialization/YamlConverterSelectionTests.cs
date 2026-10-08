using System;
using System.Text;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SharpYaml.Serialization;

namespace SharpYaml.Tests.Serialization;

[TestClass]
public sealed class YamlConverterSelectionTests
{
    [TestMethod]
    public void GetConverter_UsesFirstMatchingConverter()
    {
        var options = new YamlSerializerOptions
        {
            Converters =
            [
                new AlwaysInt32Converter("first"),
                new AlwaysInt32Converter("second"),
            ],
        };

        var writer = new YamlWriter(new StringBuilder(), options);
        var converter = writer.GetConverter(typeof(int));

        Assert.IsInstanceOfType<AlwaysInt32Converter>(converter);
        Assert.AreEqual("first", ((AlwaysInt32Converter)converter).Id);
    }

    [TestMethod]
    public void GetConverter_ExpandsFactoryConverters()
    {
        var options = new YamlSerializerOptions
        {
            Converters =
            [
                new Int32FactoryConverter(),
            ],
        };

        var writer = new YamlWriter(new StringBuilder(), options);
        var converter = writer.GetConverter(typeof(int));

        Assert.IsInstanceOfType<AlwaysInt32Converter>(converter);
        Assert.AreEqual("factory", ((AlwaysInt32Converter)converter).Id);
    }

    [TestMethod]
    public void TryGetCustomConverter_ReturnsFalseWhenNoConverterFound()
    {
        var options = new YamlSerializerOptions();

        var writer = new YamlWriter(new StringBuilder(), options);
        Assert.IsFalse(writer.TryGetCustomConverter(typeof(int), out var converter));
        Assert.IsNull(converter);
    }

    [TestMethod]
    public void TryGetCustomConverter_MatchesConverterRegisteredForUnderlyingTypeAgainstNullableLookup()
    {
        // Regression for https://github.com/xoofx/SharpYaml/issues/167: YamlConverter<T>.CanConvert is sealed
        // to an exact type match, so a converter written against a value type T must still be selected when
        // the requested type is T? - see CanConvertNullable.
        var options = new YamlSerializerOptions
        {
            Converters = [new AlwaysInt32Converter("underlying")],
        };

        var writer = new YamlWriter(new StringBuilder(), options);
        Assert.IsTrue(writer.TryGetCustomConverter(typeof(int?), out var converter));
        Assert.IsInstanceOfType<AlwaysInt32Converter>(converter);
        Assert.AreEqual("underlying", ((AlwaysInt32Converter)converter!).Id);
    }

    [TestMethod]
    public void CanConvertNullable_ConverterForValueType_MatchesNullableOfSameType()
    {
        var converter = new AlwaysInt32Converter("id");

        Assert.IsTrue(converter.CanConvertNullable(typeof(int)));
        Assert.IsTrue(converter.CanConvertNullable(typeof(int?)));
        Assert.IsFalse(converter.CanConvertNullable(typeof(long)));
        Assert.IsFalse(converter.CanConvertNullable(typeof(long?)));
    }

    private sealed class AlwaysInt32Converter : YamlConverter<int>
    {
        public AlwaysInt32Converter(string id) => Id = id;

        public string Id { get; }

        public override int Read(YamlReader reader) => 42;

        public override void Write(YamlWriter writer, int value)
        {
        }
    }

    private sealed class Int32FactoryConverter : YamlConverterFactory
    {
        public override bool CanConvert(Type typeToConvert) => typeToConvert == typeof(int);

        public override YamlConverter CreateConverter(Type typeToConvert, YamlSerializerOptions options)
            => new AlwaysInt32Converter("factory");
    }
}
