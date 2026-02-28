#nullable enable

using System;
using System.Text.Json.Serialization;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SharpYaml.Serialization;

namespace SharpYaml.Tests.Serialization;

internal sealed class GeneratedPerson
{
    [JsonPropertyName("first_name")]
    public string FirstName { get; set; } = string.Empty;

    public int Age { get; set; }
}

internal sealed class GeneratedContainer
{
    public GeneratedPerson? Person { get; set; }
}

internal sealed class GeneratedWithDefaultOptions
{
    public string DisplayName { get; set; } = string.Empty;

    public string? Optional { get; set; }
}

internal enum GeneratedColor
{
    Red = 1,
    Green = 2,
    Blue = 3,
}

internal sealed class GeneratedPrimitives
{
    public bool BoolValue { get; set; }
    public byte ByteValue { get; set; }
    public sbyte SByteValue { get; set; }
    public short Int16Value { get; set; }
    public ushort UInt16Value { get; set; }
    public int Int32Value { get; set; }
    public uint UInt32Value { get; set; }
    public long Int64Value { get; set; }
    public ulong UInt64Value { get; set; }
    public nint NIntValue { get; set; }
    public nuint NUIntValue { get; set; }
    public float SingleValue { get; set; }
    public double DoubleValue { get; set; }
    public decimal DecimalValue { get; set; }
    public char CharValue { get; set; }
    public GeneratedColor Color { get; set; }
    public int? NullableInt { get; set; }
    public GeneratedColor? NullableColor { get; set; }
}

#pragma warning disable SYSLIB1224
[JsonSerializable(typeof(GeneratedPerson))]
[JsonSerializable(typeof(GeneratedContainer))]
[JsonSerializable(typeof(GeneratedPrimitives))]
[JsonSerializable(typeof(GeneratedColor))]
[JsonSerializable(typeof(int?))]
internal partial class TestYamlSerializerContext : YamlSerializerContext
{
}

[JsonSourceGenerationOptions(
    WriteIndented = false,
    PropertyNameCaseInsensitive = true,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DictionaryKeyPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(GeneratedWithDefaultOptions))]
internal partial class TestYamlSerializerContextWithOptions : YamlSerializerContext
{
}
#pragma warning restore SYSLIB1224

[TestClass]
public class YamlSerializerSourceGenerationTests
{
    [TestMethod]
    public void GeneratedContextProvidesTypedMetadata()
    {
        var context = new TestYamlSerializerContext();
        var typeInfo = context.GetTypeInfo<GeneratedPerson>();
        var yaml = YamlSerializer.Serialize(
            new GeneratedPerson
            {
                FirstName = "Ada",
                Age = 37,
            },
            typeInfo);
        var person = YamlSerializer.Deserialize(yaml, typeInfo);

        StringAssert.Contains(yaml, "first_name");
        Assert.IsNotNull(person);
        Assert.AreEqual("Ada", person.FirstName);
        Assert.AreEqual(37, person.Age);
    }

    [TestMethod]
    public void GeneratedContextWorksAsResolver()
    {
        var context = new TestYamlSerializerContext();
        var yaml = YamlSerializer.Serialize(
            new GeneratedContainer
            {
                Person = new GeneratedPerson
                {
                    FirstName = "Ada",
                    Age = 37,
                },
            },
            typeof(GeneratedContainer),
            context.Options);
        var container = (GeneratedContainer?)YamlSerializer.Deserialize(yaml, typeof(GeneratedContainer), context.Options);

        Assert.IsNotNull(container);
        Assert.IsNotNull(container.Person);
        Assert.AreEqual("Ada", container.Person.FirstName);
        Assert.AreEqual(37, container.Person.Age);
    }

    [TestMethod]
    public void GeneratedContextDefaultAppliesJsonSourceGenerationOptions()
    {
        var context = TestYamlSerializerContextWithOptions.Default;
        var options = context.Options;

        Assert.IsFalse(options.WriteIndented);
        Assert.IsTrue(options.PropertyNameCaseInsensitive);
        Assert.AreEqual(YamlIgnoreCondition.WhenWritingNull, options.DefaultIgnoreCondition);
        Assert.AreSame(YamlNamingPolicy.CamelCase, options.PropertyNamingPolicy);
        Assert.AreSame(YamlNamingPolicy.CamelCase, options.DictionaryKeyPolicy);
        Assert.AreSame(context, options.TypeInfoResolver);

        var yaml = YamlSerializer.Serialize(
            new GeneratedWithDefaultOptions
            {
                DisplayName = "Ada",
                Optional = null,
            },
            typeof(GeneratedWithDefaultOptions),
            options);

        StringAssert.Contains(yaml, "displayName: Ada");
        Assert.IsFalse(yaml.Contains("optional:", StringComparison.Ordinal));
    }

    [TestMethod]
    public void GeneratedContextRoundTripsPrimitiveMembers()
    {
        var context = new TestYamlSerializerContext();
        var typeInfo = context.GetTypeInfo<GeneratedPrimitives>();

        var value = new GeneratedPrimitives
        {
            BoolValue = true,
            ByteValue = 255,
            SByteValue = -12,
            Int16Value = short.MinValue,
            UInt16Value = ushort.MaxValue,
            Int32Value = int.MinValue,
            UInt32Value = uint.MaxValue,
            Int64Value = long.MinValue,
            UInt64Value = ulong.MaxValue,
            NIntValue = (nint)1234,
            NUIntValue = (nuint)5678,
            SingleValue = float.PositiveInfinity,
            DoubleValue = double.NaN,
            DecimalValue = 79228162514264337593543950335m,
            CharValue = 'Z',
            Color = GeneratedColor.Green,
            NullableInt = 42,
            NullableColor = GeneratedColor.Blue,
        };

        var yaml = YamlSerializer.Serialize(value, typeInfo);
        var roundtripped = YamlSerializer.Deserialize(yaml, typeInfo);

        Assert.IsNotNull(roundtripped);
        Assert.AreEqual(value.BoolValue, roundtripped.BoolValue);
        Assert.AreEqual(value.ByteValue, roundtripped.ByteValue);
        Assert.AreEqual(value.SByteValue, roundtripped.SByteValue);
        Assert.AreEqual(value.Int16Value, roundtripped.Int16Value);
        Assert.AreEqual(value.UInt16Value, roundtripped.UInt16Value);
        Assert.AreEqual(value.Int32Value, roundtripped.Int32Value);
        Assert.AreEqual(value.UInt32Value, roundtripped.UInt32Value);
        Assert.AreEqual(value.Int64Value, roundtripped.Int64Value);
        Assert.AreEqual(value.UInt64Value, roundtripped.UInt64Value);
        Assert.AreEqual(value.NIntValue, roundtripped.NIntValue);
        Assert.AreEqual(value.NUIntValue, roundtripped.NUIntValue);
        Assert.IsTrue(float.IsPositiveInfinity(roundtripped.SingleValue));
        Assert.IsTrue(double.IsNaN(roundtripped.DoubleValue));
        Assert.AreEqual(value.DecimalValue, roundtripped.DecimalValue);
        Assert.AreEqual(value.CharValue, roundtripped.CharValue);
        Assert.AreEqual(value.Color, roundtripped.Color);
        Assert.AreEqual(value.NullableInt, roundtripped.NullableInt);
        Assert.AreEqual(value.NullableColor, roundtripped.NullableColor);

        StringAssert.Contains(yaml, "ByteValue: 255");
        StringAssert.Contains(yaml, "SingleValue: .inf");
        StringAssert.Contains(yaml, "Color: Green");
    }

    [TestMethod]
    public void GeneratedContextSupportsRootEnumAndNullable()
    {
        var context = new TestYamlSerializerContext();

        var enumTypeInfo = context.GetTypeInfo<GeneratedColor>();
        var yamlEnum = YamlSerializer.Serialize(GeneratedColor.Red, enumTypeInfo);
        Assert.AreEqual("Red\n", yamlEnum);
        Assert.AreEqual(GeneratedColor.Red, YamlSerializer.Deserialize(yamlEnum, enumTypeInfo));

        var nullableTypeInfo = context.GetTypeInfo<int?>();
        var yamlValue = YamlSerializer.Serialize((int?)123, nullableTypeInfo);
        Assert.AreEqual("123\n", yamlValue);
        Assert.AreEqual(123, YamlSerializer.Deserialize(yamlValue, nullableTypeInfo));

        var yamlNull = YamlSerializer.Serialize((int?)null, nullableTypeInfo);
        Assert.AreEqual("null\n", yamlNull);
        Assert.IsNull(YamlSerializer.Deserialize(yamlNull, nullableTypeInfo));
    }
}
