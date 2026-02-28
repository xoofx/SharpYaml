using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SharpYaml.Serialization;

namespace SharpYaml.Tests.Serialization;

[TestClass]
public sealed class YamlConverterSelectionTests
{
    [TestMethod]
    public void GetConverter_UsesFirstMatchingConverter()
    {
        var options = new YamlSerializerOptions();
        options.Converters.Add(new AlwaysInt32Converter("first"));
        options.Converters.Add(new AlwaysInt32Converter("second"));

        var converter = options.GetConverter(typeof(int));

        Assert.IsInstanceOfType<AlwaysInt32Converter>(converter);
        Assert.AreEqual("first", ((AlwaysInt32Converter)converter).Id);
    }

    [TestMethod]
    public void GetConverter_ExpandsFactoryConverters()
    {
        var options = new YamlSerializerOptions();
        options.Converters.Add(new Int32FactoryConverter());

        var converter = options.GetConverter(typeof(int));

        Assert.IsInstanceOfType<AlwaysInt32Converter>(converter);
        Assert.AreEqual("factory", ((AlwaysInt32Converter)converter).Id);
    }

    [TestMethod]
    public void GetConverter_ThrowsWhenNoConverterFound()
    {
        var options = new YamlSerializerOptions();

        var ex = Assert.Throws<NotSupportedException>(() => options.GetConverter(typeof(int)));
        StringAssert.Contains(ex.Message, "No YAML converter is registered");
    }

    private sealed class AlwaysInt32Converter : YamlConverter<int>
    {
        public AlwaysInt32Converter(string id) => Id = id;

        public string Id { get; }

        public override int Read(ref YamlReader reader, YamlSerializerOptions options) => 42;

        public override void Write(YamlWriter writer, int value, YamlSerializerOptions options)
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
