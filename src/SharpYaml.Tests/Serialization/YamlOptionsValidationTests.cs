#nullable enable

using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SharpYaml.Serialization;

namespace SharpYaml.Tests.Serialization;

[TestClass]
public sealed class YamlOptionsValidationTests
{
    private sealed class StringOnlyConverter : YamlConverter<string>
    {
        public override string Read(YamlReader reader) => throw new NotSupportedException();

        public override void Write(YamlWriter writer, string value) => throw new NotSupportedException();
    }

    private sealed class BadFactoryReturnsNull : YamlConverterFactory
    {
        public override bool CanConvert(Type typeToConvert) => typeToConvert == typeof(int);

        public override YamlConverter CreateConverter(Type typeToConvert, YamlSerializerOptions options) => null!;
    }

    private sealed class BadFactoryWrongConverter : YamlConverterFactory
    {
        public override bool CanConvert(Type typeToConvert) => typeToConvert == typeof(int);

        public override YamlConverter CreateConverter(Type typeToConvert, YamlSerializerOptions options) => new StringOnlyConverter();
    }

    [TestMethod]
    public void Options_IndentSize_MustBeAtLeastOne()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new YamlSerializerOptions { IndentSize = 0 });
    }

    [TestMethod]
    public void Options_Converters_CannotBeNullOrContainNull()
    {
        Assert.Throws<ArgumentNullException>(() => new YamlSerializerOptions { Converters = null! });
        Assert.Throws<ArgumentException>(() => new YamlSerializerOptions { Converters = new List<YamlConverter> { null! } });
    }

    [TestMethod]
    public void ConverterFactory_MustReturnValidConverter()
    {
        var options1 = new YamlSerializerOptions
        {
            Converters = new YamlConverter[] { new BadFactoryReturnsNull() },
        };
        Assert.Throws<InvalidOperationException>(() => new YamlWriter(new StringBuilder(), options1).GetConverter(typeof(int)));

        var options2 = new YamlSerializerOptions
        {
            Converters = new YamlConverter[] { new BadFactoryWrongConverter() },
        };
        Assert.Throws<InvalidOperationException>(() => new YamlWriter(new StringBuilder(), options2).GetConverter(typeof(int)));
    }

    [TestMethod]
    public void NamingPolicy_CamelCase_ConvertsOnlyWhenLeadingUppercase()
    {
        Assert.AreEqual(string.Empty, JsonNamingPolicy.CamelCase.ConvertName(string.Empty));
        Assert.AreEqual("already", JsonNamingPolicy.CamelCase.ConvertName("already"));
        Assert.AreEqual("hello", JsonNamingPolicy.CamelCase.ConvertName("Hello"));
        Assert.AreEqual("urlValue", JsonNamingPolicy.CamelCase.ConvertName("URLValue"));
    }

    [TestMethod]
    public void PolymorphismOptions_ValidateDiscriminatorStyle()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new YamlPolymorphismOptions
        {
            DiscriminatorStyle = YamlTypeDiscriminatorStyle.Unspecified,
        });
    }

    [TestMethod]
    public void PolymorphismOptions_ValidatePropertyName()
    {
        Assert.Throws<ArgumentException>(() => new YamlPolymorphismOptions { TypeDiscriminatorPropertyName = string.Empty });
        Assert.Throws<ArgumentException>(() => new YamlPolymorphismOptions { TypeDiscriminatorPropertyName = null! });
    }
}
