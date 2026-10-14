using System;
using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SharpYaml.Serialization;

namespace SharpYaml.Tests.Serialization;

[TestClass]
public sealed class YamlNullableConverterTests
{
    private static readonly Guid Value = Guid.Parse("12345678-1234-1234-1234-123456789abc");

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void BuiltInNullableConverter_AcceptsNonGenericUnderlyingConverter(bool factory)
    {
        var options = new YamlSerializerOptions
        {
            Converters = [factory ? new UnderlyingGuidFactory() : new UntypedGuidConverter()],
        };
        Assert.AreEqual(Value, YamlSerializer.Deserialize<Guid?>("custom", options));
        StringAssert.Contains(YamlSerializer.Serialize<Guid?>(Value, options), "custom");
        Assert.IsNull(YamlSerializer.Deserialize<Guid?>("null", options));
        Assert.IsNull(YamlSerializer.Deserialize<Guid?>(YamlSerializer.Serialize<Guid?>(null, options), options));
    }

    [TestMethod]
    [DataRow(false, false)]
    [DataRow(false, true)]
    [DataRow(true, false)]
    [DataRow(true, true)]
    public void UnderlyingConverter_ReadsNullableRootsMembersAndElements(bool generated, bool nonGeneric)
    {
        var options = CreateOptions(generated, nonGeneric ? new UntypedGuidConverter() : new GuidConverter());
        Assert.AreEqual(Value, YamlSerializer.Deserialize<Guid?>("custom", options));
        foreach (var yaml in new[] { "null", "~", "!!null null" })
        {
            Assert.IsNull(YamlSerializer.Deserialize<Guid?>(yaml, options));
        }

        var model = YamlSerializer.Deserialize<NullableConverterModel>(
            "Value: custom\nEmpty: null\nItems: [custom, null, custom]\nMap: {present: custom, absent: null}\n", options)!;
        Assert.AreEqual(Value, model.Value);
        Assert.IsNull(model.Empty);
        CollectionAssert.AreEqual(new Guid?[] { Value, null, Value }, model.Items);
        Assert.AreEqual(Value, model.Map["present"]);
        Assert.IsNull(model.Map["absent"]);
        CollectionAssert.AreEqual(new Guid?[] { Value, null }, YamlSerializer.Deserialize<Guid?[]>("[custom, null]", options));
        CollectionAssert.AreEqual(new Guid?[] { Value, null }, YamlSerializer.Deserialize<List<Guid?>>("[custom, null]", options));
        var map = YamlSerializer.Deserialize<Dictionary<string, Guid?>>("{present: custom, absent: null}", options)!;
        Assert.AreEqual(Value, map["present"]);
        Assert.IsNull(map["absent"]);
    }

    [TestMethod]
    [DataRow(false, false)]
    [DataRow(false, true)]
    [DataRow(true, false)]
    [DataRow(true, true)]
    public void UnderlyingConverter_WritesNullableRootsMembersAndElements(bool generated, bool nonGeneric)
    {
        var options = CreateOptions(generated, nonGeneric ? new UntypedGuidConverter() : new GuidConverter());
        StringAssert.Contains(YamlSerializer.Serialize<Guid?>(Value, options), "custom");
        Assert.IsNull(YamlSerializer.Deserialize<Guid?>(YamlSerializer.Serialize<Guid?>(null, options), options));
        var yaml = YamlSerializer.Serialize(new NullableConverterModel
        {
            Value = Value,
            Items = [Value, null, Value],
            Map = new() { ["present"] = Value, ["absent"] = null },
        }, options);
        StringAssert.Contains(yaml, "Value: custom");
        var model = YamlSerializer.Deserialize<NullableConverterModel>(yaml, options)!;
        Assert.AreEqual(Value, model.Value);
        Assert.IsNull(model.Empty);
        CollectionAssert.AreEqual(new Guid?[] { Value, null, Value }, model.Items);
        Assert.AreEqual(Value, model.Map["present"]);
        Assert.IsNull(model.Map["absent"]);
        var arrayYaml = YamlSerializer.Serialize(new Guid?[] { Value, null }, options);
        CollectionAssert.AreEqual(new Guid?[] { Value, null }, YamlSerializer.Deserialize<Guid?[]>(arrayYaml, options));
        var mapYaml = YamlSerializer.Serialize(model.Map, options);
        StringAssert.Contains(mapYaml, "present: custom");
        Assert.IsNull(YamlSerializer.Deserialize<Dictionary<string, Guid?>>(mapYaml, options)!["absent"]);
    }

    [TestMethod]
    [DataRow(false, false, false)]
    [DataRow(false, true, false)]
    [DataRow(true, false, false)]
    [DataRow(true, true, false)]
    [DataRow(false, false, true)]
    [DataRow(false, true, true)]
    [DataRow(true, false, true)]
    [DataRow(true, true, true)]
    public void ExactNullableConverter_WinsInEitherOrderAndHandlesNull(bool generated, bool reversed, bool factory)
    {
        YamlConverter exact = factory ? new NullableGuidFactory() : new NullableGuidConverter();
        var options = CreateOptions(generated, reversed ? [exact, new GuidConverter()] : [new GuidConverter(), exact]);
        Assert.AreEqual(Guid.Empty, YamlSerializer.Deserialize<Guid?>("null", options));
        Assert.AreEqual(Guid.Empty, YamlSerializer.Deserialize<Guid?>("custom", options));
        StringAssert.Contains(YamlSerializer.Serialize<Guid?>(null, options), "exact-null");
        StringAssert.Contains(YamlSerializer.Serialize<Guid?>(Value, options), "exact-value");
        var model = YamlSerializer.Deserialize<NullableConverterModel>("Value: custom\nItems: [null]\nMap: {key: null}", options)!;
        Assert.AreEqual(Guid.Empty, model.Value);
        Assert.AreEqual(Guid.Empty, model.Items[0]);
        Assert.AreEqual(Guid.Empty, model.Map["key"]);
        var yaml = YamlSerializer.Serialize(new NullableConverterModel { Items = [null], Map = new() { ["key"] = null } }, options);
        StringAssert.Contains(yaml, "Value: exact-null");
        StringAssert.Contains(yaml, "key: exact-null");
    }

    private static YamlSerializerOptions CreateOptions(bool generated, params YamlConverter[] converters)
        => new()
        {
            TypeInfoResolver = generated ? NullableConverterContext.Default : ReflectionYamlTypeInfoResolver.Default,
            Converters = converters,
        };

    private sealed class GuidConverter : YamlConverter<Guid>
    {
        public override Guid Read(YamlReader reader)
        {
            Assert.AreEqual("custom", reader.GetScalarValue());
            reader.Read();
            return Value;
        }

        public override void Write(YamlWriter writer, Guid value)
        {
            Assert.AreEqual(Value, value);
            writer.WriteScalar("custom");
        }
    }

    private sealed class UntypedGuidConverter : YamlConverter
    {
        public override bool CanConvert(Type typeToConvert) => typeToConvert == typeof(Guid);

        public override object Read(YamlReader reader, Type typeToConvert)
        {
            // A non-generic converter must receive the type that it agreed to convert.
            Assert.AreEqual(typeof(Guid), typeToConvert);
            Assert.AreEqual("custom", reader.GetScalarValue());
            reader.Read();
            return Value;
        }

        public override void Write(YamlWriter writer, object value)
        {
            Assert.AreEqual(Value, value);
            writer.WriteScalar("custom");
        }
    }

    private sealed class NullableGuidConverter : YamlConverter<Guid?>
    {
        public override Guid? Read(YamlReader reader)
        {
            reader.Read();
            return Guid.Empty;
        }

        public override void Write(YamlWriter writer, Guid? value)
            => writer.WriteScalar(value.HasValue ? "exact-value" : "exact-null");
    }

    private sealed class NullableGuidFactory : YamlConverterFactory
    {
        public override bool CanConvert(Type typeToConvert) => typeToConvert == typeof(Guid?);

        public override YamlConverter CreateConverter(Type typeToConvert, YamlSerializerOptions options)
        {
            Assert.AreEqual(typeof(Guid?), typeToConvert);
            return new NullableGuidConverter();
        }
    }

    private sealed class UnderlyingGuidFactory : YamlConverterFactory
    {
        public override bool CanConvert(Type typeToConvert) => typeToConvert == typeof(Guid);

        public override YamlConverter CreateConverter(Type typeToConvert, YamlSerializerOptions options)
        {
            Assert.AreEqual(typeof(Guid), typeToConvert);
            return new UntypedGuidConverter();
        }
    }
}

internal sealed class NullableConverterModel
{
    public Guid? Value { get; set; }
    public Guid? Empty { get; set; }
    public List<Guid?> Items { get; set; } = [];
    public Dictionary<string, Guid?> Map { get; set; } = [];
}

[YamlSerializable(typeof(Guid?))]
[YamlSerializable(typeof(Guid?[]))]
[YamlSerializable(typeof(List<Guid?>))]
[YamlSerializable(typeof(Dictionary<string, Guid?>))]
[YamlSerializable(typeof(NullableConverterModel))]
internal partial class NullableConverterContext : YamlSerializerContext
{
}
