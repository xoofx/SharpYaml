#nullable enable

using System;
using System.Collections.Generic;
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

internal sealed class GeneratedCollections
{
    public int[] Numbers { get; set; } = Array.Empty<int>();

    public List<string> Names { get; set; } = new();

    public Dictionary<string, int> Map { get; set; } = new();

    public List<GeneratedPerson> People { get; set; } = new();

    public Dictionary<string, GeneratedPerson> PeopleByName { get; set; } = new();
}

internal sealed class GeneratedReferenceNode
{
    public string Name { get; set; } = string.Empty;

    public GeneratedReferenceNode? Next { get; set; }
}

internal sealed class GeneratedReferenceContainer
{
    public GeneratedReferenceNode? First { get; set; }

    public GeneratedReferenceNode? Second { get; set; }
}

[JsonPolymorphic(TypeDiscriminatorPropertyName = "$type")]
[JsonDerivedType(typeof(GeneratedDog), "dog")]
[JsonDerivedType(typeof(GeneratedCat), "cat")]
internal abstract class GeneratedAnimal
{
    public string Name { get; set; } = string.Empty;
}

internal sealed class GeneratedDog : GeneratedAnimal
{
    public int BarkVolume { get; set; }
}

internal sealed class GeneratedCat : GeneratedAnimal
{
    public bool LikesCream { get; set; }
}

internal sealed class GeneratedZoo
{
    public GeneratedAnimal? Animal { get; set; }
}

[YamlPolymorphic(DiscriminatorStyle = YamlTypeDiscriminatorStyle.Tag)]
[YamlDerivedType(typeof(GeneratedTaggedDog), "dog", Tag = "!dog")]
internal abstract class GeneratedTaggedAnimal
{
    public string Name { get; set; } = string.Empty;
}

internal sealed class GeneratedTaggedDog : GeneratedTaggedAnimal
{
    public int BarkVolume { get; set; }
}

internal sealed class GeneratedTaggedZoo
{
    public GeneratedTaggedAnimal? Animal { get; set; }
}

internal sealed class ConstantIntConverter : YamlConverter<int>
{
    public override int Read(ref YamlReader reader, YamlSerializerOptions options)
    {
        reader.Skip();
        return 123;
    }

    public override void Write(YamlWriter writer, int value, YamlSerializerOptions options)
        => writer.WriteScalar("123");
}

#pragma warning disable SYSLIB1224
[JsonSerializable(typeof(GeneratedPerson))]
[JsonSerializable(typeof(GeneratedContainer))]
[JsonSerializable(typeof(GeneratedPrimitives))]
[JsonSerializable(typeof(GeneratedColor))]
[JsonSerializable(typeof(int?))]
[JsonSerializable(typeof(GeneratedCollections))]
[JsonSerializable(typeof(List<int>))]
[JsonSerializable(typeof(Dictionary<string, int>))]
[JsonSerializable(typeof(int[]))]
[JsonSerializable(typeof(GeneratedReferenceNode))]
[JsonSerializable(typeof(GeneratedReferenceContainer))]
[JsonSerializable(typeof(GeneratedAnimal))]
[JsonSerializable(typeof(GeneratedZoo))]
[JsonSerializable(typeof(GeneratedTaggedAnimal))]
[JsonSerializable(typeof(GeneratedTaggedZoo))]
internal partial class TestYamlSerializerContext : YamlSerializerContext
{
    public TestYamlSerializerContext()
    {
    }

    public TestYamlSerializerContext(YamlSerializerOptions options)
        : base(options)
    {
    }
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
    public TestYamlSerializerContextWithOptions()
    {
    }

    public TestYamlSerializerContextWithOptions(YamlSerializerOptions options)
        : base(options)
    {
    }
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
    public void GeneratedContextCanBeUsedDirectlyWithSerializerOverloads()
    {
        var context = TestYamlSerializerContext.Default;
        var value = new GeneratedPerson
        {
            FirstName = "Ada",
            Age = 37,
        };

        var yaml = YamlSerializer.Serialize(value, typeof(GeneratedPerson), context);
        var person = (GeneratedPerson?)YamlSerializer.Deserialize(yaml, typeof(GeneratedPerson), context);

        StringAssert.Contains(yaml, "first_name: Ada");
        Assert.IsNotNull(person);
        Assert.AreEqual("Ada", person.FirstName);
        Assert.AreEqual(37, person.Age);
    }

    [TestMethod]
    public void GenericSerializerUsesTypeInfoResolverFromOptions()
    {
        var options = new YamlSerializerOptions
        {
            TypeInfoResolver = TestYamlSerializerContext.Default,
        };

        var value = new GeneratedPerson
        {
            FirstName = "Ada",
            Age = 37,
        };

        var yaml = YamlSerializer.Serialize(value, options);
        var person = YamlSerializer.Deserialize<GeneratedPerson>(yaml, options);

        StringAssert.Contains(yaml, "first_name: Ada");
        Assert.IsNotNull(person);
        Assert.AreEqual("Ada", person.FirstName);
        Assert.AreEqual(37, person.Age);
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

    [TestMethod]
    public void GeneratedContextRoundTripsCollectionMembers()
    {
        var context = new TestYamlSerializerContext();
        var typeInfo = context.GetTypeInfo<GeneratedCollections>();

        var value = new GeneratedCollections
        {
            Numbers = new[] { 1, 2, 3 },
            Names = new List<string> { "Ada", "Bob" },
            Map = new Dictionary<string, int>
            {
                ["one"] = 1,
                ["two"] = 2,
            },
            People = new List<GeneratedPerson>
            {
                new GeneratedPerson { FirstName = "Ada", Age = 37 },
                new GeneratedPerson { FirstName = "Bob", Age = 28 },
            },
            PeopleByName = new Dictionary<string, GeneratedPerson>
            {
                ["ada"] = new GeneratedPerson { FirstName = "Ada", Age = 37 },
            },
        };

        var yaml = YamlSerializer.Serialize(value, typeInfo);
        var roundtripped = YamlSerializer.Deserialize(yaml, typeInfo);

        Assert.IsNotNull(roundtripped);
        CollectionAssert.AreEqual(value.Numbers, roundtripped.Numbers);
        CollectionAssert.AreEqual(value.Names, roundtripped.Names);
        Assert.AreEqual(2, roundtripped.Map.Count);
        Assert.AreEqual(1, roundtripped.Map["one"]);
        Assert.AreEqual(2, roundtripped.Map["two"]);
        Assert.AreEqual(2, roundtripped.People.Count);
        Assert.AreEqual("Ada", roundtripped.People[0].FirstName);
        Assert.AreEqual(37, roundtripped.People[0].Age);
        Assert.AreEqual("Bob", roundtripped.People[1].FirstName);
        Assert.AreEqual(28, roundtripped.People[1].Age);
        Assert.AreEqual("Ada", roundtripped.PeopleByName["ada"].FirstName);

        StringAssert.Contains(yaml, "Numbers:");
        StringAssert.Contains(yaml, "- 1");
        StringAssert.Contains(yaml, "Map:");
        StringAssert.Contains(yaml, "People:");
    }

    [TestMethod]
    public void GeneratedContextSupportsRootCollections()
    {
        var context = new TestYamlSerializerContext();

        var listTypeInfo = context.GetTypeInfo<List<int>>();
        var yamlList = YamlSerializer.Serialize(new List<int> { 1, 2, 3 }, listTypeInfo);
        var list = YamlSerializer.Deserialize(yamlList, listTypeInfo);
        Assert.IsNotNull(list);
        CollectionAssert.AreEqual(new[] { 1, 2, 3 }, list);

        var arrayTypeInfo = context.GetTypeInfo<int[]>();
        var yamlArray = YamlSerializer.Serialize(new[] { 4, 5 }, arrayTypeInfo);
        CollectionAssert.AreEqual(new[] { 4, 5 }, YamlSerializer.Deserialize(yamlArray, arrayTypeInfo));

        var dictTypeInfo = context.GetTypeInfo<Dictionary<string, int>>();
        var yamlDict = YamlSerializer.Serialize(new Dictionary<string, int> { ["a"] = 1, ["b"] = 2 }, dictTypeInfo);
        var dict = YamlSerializer.Deserialize(yamlDict, dictTypeInfo);
        Assert.IsNotNull(dict);
        Assert.AreEqual(1, dict["a"]);
        Assert.AreEqual(2, dict["b"]);
    }

    [TestMethod]
    public void GeneratedContextHonorsCustomConverters()
    {
        var context = new TestYamlSerializerContext(
            new YamlSerializerOptions
            {
                Converters =
                [
                    new ConstantIntConverter(),
                ],
            });

        var primitivesTypeInfo = context.GetTypeInfo<GeneratedPrimitives>();
        var yaml = YamlSerializer.Serialize(new GeneratedPrimitives { Int32Value = 5 }, primitivesTypeInfo);
        StringAssert.Contains(yaml, "Int32Value: 123");

        var roundtripped = YamlSerializer.Deserialize(yaml, primitivesTypeInfo);
        Assert.IsNotNull(roundtripped);
        Assert.AreEqual(123, roundtripped.Int32Value);

        var listTypeInfo = context.GetTypeInfo<List<int>>();
        var yamlList = YamlSerializer.Serialize(new List<int> { 1, 2 }, listTypeInfo);
        StringAssert.Contains(yamlList, "- 123");
        var list = YamlSerializer.Deserialize(yamlList, listTypeInfo);
        Assert.IsNotNull(list);
        CollectionAssert.AreEqual(new[] { 123, 123 }, list);
    }

    [TestMethod]
    public void GeneratedContextPreservesSharedReferences()
    {
        var context = new TestYamlSerializerContext(
            new YamlSerializerOptions
            {
                ReferenceHandling = YamlReferenceHandling.Preserve,
            });

        var shared = new GeneratedReferenceNode { Name = "shared" };
        var container = new GeneratedReferenceContainer { First = shared, Second = shared };

        var typeInfo = context.GetTypeInfo<GeneratedReferenceContainer>();
        var yaml = YamlSerializer.Serialize(container, typeInfo);
        StringAssert.Contains(yaml, "&id002");
        StringAssert.Contains(yaml, "*id002");

        var roundtripped = YamlSerializer.Deserialize(yaml, typeInfo);
        Assert.IsNotNull(roundtripped);
        Assert.IsNotNull(roundtripped.First);
        Assert.IsNotNull(roundtripped.Second);
        Assert.IsTrue(ReferenceEquals(roundtripped.First, roundtripped.Second));
        Assert.AreEqual("shared", roundtripped.First.Name);
    }

    [TestMethod]
    public void GeneratedContextPreservesCycles()
    {
        var context = new TestYamlSerializerContext(
            new YamlSerializerOptions
            {
                ReferenceHandling = YamlReferenceHandling.Preserve,
            });

        var node = new GeneratedReferenceNode { Name = "self" };
        node.Next = node;

        var typeInfo = context.GetTypeInfo<GeneratedReferenceNode>();
        var yaml = YamlSerializer.Serialize(node, typeInfo);
        StringAssert.Contains(yaml, "&id001");
        StringAssert.Contains(yaml, "*id001");

        var roundtripped = YamlSerializer.Deserialize(yaml, typeInfo);
        Assert.IsNotNull(roundtripped);
        Assert.IsNotNull(roundtripped.Next);
        Assert.IsTrue(ReferenceEquals(roundtripped, roundtripped.Next));
    }

    [TestMethod]
    public void GeneratedContextSupportsPolymorphism_PropertyDiscriminator()
    {
        var context = new TestYamlSerializerContext();

        var typeInfo = context.GetTypeInfo<GeneratedZoo>();
        var yaml = YamlSerializer.Serialize(
            new GeneratedZoo
            {
                Animal = new GeneratedDog { Name = "Rex", BarkVolume = 7 },
            },
            typeInfo);

        StringAssert.Contains(yaml, "$type: dog");
        StringAssert.Contains(yaml, "BarkVolume: 7");

        var roundtripped = YamlSerializer.Deserialize(yaml, typeInfo);
        Assert.IsNotNull(roundtripped);
        Assert.IsInstanceOfType(roundtripped.Animal, typeof(GeneratedDog));
        var dog = (GeneratedDog)roundtripped.Animal!;
        Assert.AreEqual("Rex", dog.Name);
        Assert.AreEqual(7, dog.BarkVolume);
    }

    [TestMethod]
    public void GeneratedContextSupportsPolymorphism_TagDiscriminator()
    {
        var context = new TestYamlSerializerContext();

        var typeInfo = context.GetTypeInfo<GeneratedTaggedZoo>();
        var yaml = YamlSerializer.Serialize(
            new GeneratedTaggedZoo
            {
                Animal = new GeneratedTaggedDog { Name = "Rex", BarkVolume = 7 },
            },
            typeInfo);

        StringAssert.Contains(yaml, "!dog");
        Assert.IsFalse(yaml.Contains("$type", StringComparison.Ordinal));

        var roundtripped = YamlSerializer.Deserialize(yaml, typeInfo);
        Assert.IsNotNull(roundtripped);
        Assert.IsInstanceOfType(roundtripped.Animal, typeof(GeneratedTaggedDog));
        var dog = (GeneratedTaggedDog)roundtripped.Animal!;
        Assert.AreEqual("Rex", dog.Name);
        Assert.AreEqual(7, dog.BarkVolume);
    }

    [TestMethod]
    public void GeneratedContextErrorsIncludeSourceNameAndLocation()
    {
        var context = new TestYamlSerializerContext(
            new YamlSerializerOptions
            {
                SourceName = "generated.yaml",
            });

        var typeInfo = context.GetTypeInfo<GeneratedPerson>();
        var exception = Assert.Throws<YamlException>(() => YamlSerializer.Deserialize("123", typeInfo));
        Assert.AreEqual("generated.yaml", exception.SourceName);
        StringAssert.Contains(exception.Message, "Lin:");
        StringAssert.Contains(exception.Message, "Col:");
    }
}
