#nullable enable

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Text.Json;
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

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
internal sealed class GeneratedAttributedUnmappedPayload
{
    public string DisplayName { get; set; } = string.Empty;
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
internal sealed class GeneratedAttributedExtensionDataPayload
{
    public string DisplayName { get; set; } = string.Empty;

    [YamlExtensionData]
    public Dictionary<string, object?> Extra { get; set; } = new();
}

internal sealed class GeneratedSchemaAwareScalars
{
    public string? NullableText { get; set; }

    public string? QuotedText { get; set; }

    public bool PlainFlag { get; set; }

    public string? QuotedFlag { get; set; }
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

internal sealed class GeneratedWellKnownScalars
{
    public DateTime WhenUtc { get; set; }
    public DateTimeOffset WhenOffset { get; set; }
    public Guid Id { get; set; }
    public TimeSpan Duration { get; set; }
}

internal sealed class GeneratedModernScalars
{
    public DateOnly Date { get; set; }
    public TimeOnly Time { get; set; }
    public Half Ratio { get; set; }
    public Int128 Big { get; set; }
    public UInt128 UBig { get; set; }
}

internal sealed class GeneratedCollections
{
    public int[] Numbers { get; set; } = Array.Empty<int>();

    public List<string> Names { get; set; } = new();

    public Dictionary<string, int> Map { get; set; } = new();

    public List<GeneratedPerson> People { get; set; } = new();

    public Dictionary<string, GeneratedPerson> PeopleByName { get; set; } = new();
}

internal sealed class GeneratedMoreCollections
{
    public IReadOnlyList<int>? ReadOnlyNumbers { get; set; }

    public ISet<string>? Tags { get; set; }

    public Dictionary<int, string> IntKeyMap { get; set; } = new();

    public IReadOnlyDictionary<GeneratedColor, int>? EnumKeyMap { get; set; }

    public ImmutableArray<int> ImmutableNumbers { get; set; }

    public ImmutableList<string>? ImmutableNames { get; set; }
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

[JsonPolymorphic(TypeDiscriminatorPropertyName = "type")]
[JsonDerivedType(typeof(GeneratedDefaultCat), "cat")]
[JsonDerivedType(typeof(GeneratedDefaultOther))]
internal abstract class GeneratedDefaultAnimal
{
    public string Name { get; set; } = string.Empty;
}

internal sealed class GeneratedDefaultCat : GeneratedDefaultAnimal
{
    public int Lives { get; set; }
}

internal sealed class GeneratedDefaultOther : GeneratedDefaultAnimal
{
}

internal sealed class GeneratedDefaultZoo
{
    public GeneratedDefaultAnimal? Animal { get; set; }
}

[YamlPolymorphic]
[YamlDerivedType(typeof(GeneratedYamlDefaultCat), "cat")]
[YamlDerivedType(typeof(GeneratedYamlDefaultOther))]
internal abstract class GeneratedYamlDefaultAnimal
{
    public string Name { get; set; } = string.Empty;
}

internal sealed class GeneratedYamlDefaultCat : GeneratedYamlDefaultAnimal
{
    public int Lives { get; set; }
}

internal sealed class GeneratedYamlDefaultOther : GeneratedYamlDefaultAnimal
{
}

internal sealed class GeneratedYamlDefaultZoo
{
    public GeneratedYamlDefaultAnimal? Animal { get; set; }
}

[YamlPolymorphic(UnknownDerivedTypeHandling = YamlUnknownDerivedTypeHandling.FallBackToBase)]
[YamlDerivedType(typeof(GeneratedFallbackCircle), "circle")]
internal class GeneratedFallbackShape
{
    public string Name { get; set; } = string.Empty;
}

internal sealed class GeneratedFallbackCircle : GeneratedFallbackShape
{
    public double Radius { get; set; }
}

internal sealed class GeneratedFallbackZoo
{
    public GeneratedFallbackShape? Shape { get; set; }
}

[JsonPolymorphic(TypeDiscriminatorPropertyName = "$type")]
[JsonDerivedType(typeof(GeneratedJsonIntDog), 1)]
[JsonDerivedType(typeof(GeneratedJsonIntCat), 2)]
internal abstract class GeneratedJsonIntAnimal
{
    public string Name { get; set; } = string.Empty;
}

internal sealed class GeneratedJsonIntDog : GeneratedJsonIntAnimal
{
    public int BarkVolume { get; set; }
}

internal sealed class GeneratedJsonIntCat : GeneratedJsonIntAnimal
{
    public int Lives { get; set; }
}

internal sealed class GeneratedJsonIntZoo
{
    public GeneratedJsonIntAnimal? Animal { get; set; }
}

[YamlPolymorphic]
[YamlDerivedType(typeof(GeneratedYamlIntDog), 1)]
[YamlDerivedType(typeof(GeneratedYamlIntCat), 2)]
internal abstract class GeneratedYamlIntAnimal
{
    public string Name { get; set; } = string.Empty;
}

internal sealed class GeneratedYamlIntDog : GeneratedYamlIntAnimal
{
    public int BarkVolume { get; set; }
}

internal sealed class GeneratedYamlIntCat : GeneratedYamlIntAnimal
{
    public int Lives { get; set; }
}

internal sealed class GeneratedYamlIntZoo
{
    public GeneratedYamlIntAnimal? Animal { get; set; }
}

[JsonPolymorphic(TypeDiscriminatorPropertyName = "$type", UnknownDerivedTypeHandling = JsonUnknownDerivedTypeHandling.FallBackToBaseType)]
[JsonDerivedType(typeof(GeneratedJsonFallbackCircle), "circle")]
internal class GeneratedJsonFallbackShape
{
    public string Name { get; set; } = string.Empty;
}

internal sealed class GeneratedJsonFallbackCircle : GeneratedJsonFallbackShape
{
    public double Radius { get; set; }
}

internal sealed class GeneratedJsonFallbackZoo
{
    public GeneratedJsonFallbackShape? Shape { get; set; }
}

internal sealed class ConstantIntConverter : YamlConverter<int>
{
    public override int Read(YamlReader reader)
    {
        reader.Skip();
        return 123;
    }

    public override void Write(YamlWriter writer, int value)
        => writer.WriteScalar("123");
}

internal sealed class GeneratedLifecycleCallbacks : IYamlOnDeserializing, IYamlOnDeserialized, IYamlOnSerializing, IYamlOnSerialized
{
    public int Value { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.Always)]
    public int OnDeserializingCount { get; private set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.Always)]
    public int OnDeserializedCount { get; private set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.Always)]
    public int OnSerializingCount { get; private set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.Always)]
    public int OnSerializedCount { get; private set; }

    public void OnDeserializing() => OnDeserializingCount++;

    public void OnDeserialized() => OnDeserializedCount++;

    public void OnSerializing() => OnSerializingCount++;

    public void OnSerialized() => OnSerializedCount++;
}

internal sealed class GeneratedRequiredPayload
{
    [YamlRequired]
    public int RequiredValue { get; set; }
}

internal sealed class GeneratedYamlRequiredInitOnlyPayload
{
    [YamlRequired]
    public string Name { get; init; } = string.Empty;

    public int Age { get; init; }
}

internal sealed class GeneratedJsonRequiredInitOnlyPayload
{
    [JsonRequired]
    public string Name { get; init; } = string.Empty;

    public int Age { get; init; }
}

internal sealed class GeneratedOptionalInitOnlyPayload
{
    public string Name { get; init; } = "fallback";

    public int Age { get; init; } = 7;
}

internal sealed class GeneratedNullableInitOnlyPayload
{
    public string? NullableName { get; init; }

    public string NonNullableName { get; init; } = "fallback";
}

internal sealed class GeneratedExtensionDataDictionaryPayload
{
    public int Known { get; set; }

    [YamlExtensionData]
    public Dictionary<string, object?>? Extra { get; set; }
}

internal sealed class GeneratedExtensionDataMappingPayload
{
    [YamlExtensionData]
    public SharpYaml.Model.YamlMapping? Extra { get; set; }
}

internal sealed class GeneratedInitOnlyExtensionDataDictionaryPayload
{
    public int Known { get; set; }

    [YamlExtensionData]
    public Dictionary<string, object?> Extra { get; init; } = new();
}

internal sealed class GeneratedInitOnlyExtensionDataMappingPayload
{
    public int Known { get; set; }

    [YamlExtensionData]
    public SharpYaml.Model.YamlMapping Extra { get; init; } = new();
}

internal sealed class GeneratedMemberConverterPayload
{
    [YamlConverter(typeof(ConstantIntConverter))]
    public int Value { get; set; }
}

[YamlConverter(typeof(GeneratedTypeConverter))]
internal sealed class GeneratedTypeWithConverter
{
    public int Value { get; set; }
}

internal sealed class GeneratedTypeConverter : YamlConverter<GeneratedTypeWithConverter>
{
    public override GeneratedTypeWithConverter? Read(YamlReader reader)
    {
        if (reader.TokenType == YamlTokenType.Scalar && YamlScalar.IsNull(reader.ScalarValue.AsSpan()))
        {
            reader.Read();
            return null;
        }

        if (reader.TokenType != YamlTokenType.Scalar)
        {
            throw YamlThrowHelper.ThrowExpectedScalar(reader);
        }

        if (!YamlScalar.TryParseInt64(reader.ScalarValue.AsSpan(), out var parsed))
        {
            throw YamlThrowHelper.ThrowInvalidIntegerScalar(reader);
        }

        reader.Read();
        return new GeneratedTypeWithConverter { Value = (int)parsed };
    }

    public override void Write(YamlWriter writer, GeneratedTypeWithConverter value)
        => writer.WriteScalar(value.Value);
}

internal sealed class GeneratedYamlCtorModel
{
    [YamlConstructor]
    public GeneratedYamlCtorModel(string name, int age, bool ignored = false)
    {
        Name = name;
        Age = age;
    }

    public string Name { get; }

    public int Age { get; }
}

internal sealed class GeneratedJsonCtorModel
{
    [JsonConstructor]
    public GeneratedJsonCtorModel(string name, int age)
    {
        Name = name;
        Age = age;
    }

    public string Name { get; }

    public int Age { get; }
}

internal sealed class GeneratedInternalYamlCtorModel
{
    [YamlConstructor]
    internal GeneratedInternalYamlCtorModel(string name, int age)
    {
        Name = name;
        Age = age;
    }

    public string Name { get; }

    public int Age { get; }
}

internal sealed class GeneratedInternalJsonCtorModel
{
    [JsonConstructor]
    internal GeneratedInternalJsonCtorModel(string name, int age)
    {
        Name = name;
        Age = age;
    }

    public string Name { get; }

    public int Age { get; }
}

[YamlSerializable(typeof(GeneratedPerson))]
[YamlSerializable(typeof(GeneratedContainer))]
[YamlSerializable(typeof(GeneratedPrimitives))]
[YamlSerializable(typeof(GeneratedWellKnownScalars))]
[YamlSerializable(typeof(GeneratedModernScalars))]
[YamlSerializable(typeof(GeneratedColor))]
[YamlSerializable(typeof(bool))]
[YamlSerializable(typeof(int))]
[YamlSerializable(typeof(int?))]
[YamlSerializable(typeof(GeneratedCollections))]
[YamlSerializable(typeof(GeneratedMoreCollections))]
[YamlSerializable(typeof(List<int>))]
[YamlSerializable(typeof(Dictionary<string, int>))]
[YamlSerializable(typeof(Dictionary<int, int>))]
[YamlSerializable(typeof(Dictionary<int, string>))]
[YamlSerializable(typeof(IReadOnlyList<int>))]
[YamlSerializable(typeof(ISet<string>))]
[YamlSerializable(typeof(HashSet<int>))]
[YamlSerializable(typeof(IReadOnlyDictionary<GeneratedColor, int>))]
[YamlSerializable(typeof(ImmutableArray<int>))]
[YamlSerializable(typeof(ImmutableList<string>))]
[YamlSerializable(typeof(int[]))]
[YamlSerializable(typeof(GeneratedReferenceNode))]
[YamlSerializable(typeof(GeneratedReferenceContainer))]
[YamlSerializable(typeof(GeneratedAnimal))]
[YamlSerializable(typeof(GeneratedZoo))]
[YamlSerializable(typeof(GeneratedTaggedAnimal))]
[YamlSerializable(typeof(GeneratedTaggedZoo))]
[YamlSerializable(typeof(GeneratedDefaultAnimal))]
[YamlSerializable(typeof(GeneratedDefaultZoo))]
[YamlSerializable(typeof(GeneratedYamlDefaultAnimal))]
[YamlSerializable(typeof(GeneratedYamlDefaultZoo))]
[YamlSerializable(typeof(GeneratedFallbackShape))]
[YamlSerializable(typeof(GeneratedFallbackZoo))]
[YamlSerializable(typeof(GeneratedJsonIntAnimal))]
[YamlSerializable(typeof(GeneratedJsonIntZoo))]
[YamlSerializable(typeof(GeneratedYamlIntAnimal))]
[YamlSerializable(typeof(GeneratedYamlIntZoo))]
[YamlSerializable(typeof(GeneratedJsonFallbackShape))]
[YamlSerializable(typeof(GeneratedJsonFallbackZoo))]
[YamlSerializable(typeof(GeneratedLifecycleCallbacks))]
[YamlSerializable(typeof(GeneratedRequiredPayload))]
[YamlSerializable(typeof(GeneratedYamlRequiredInitOnlyPayload))]
[YamlSerializable(typeof(GeneratedJsonRequiredInitOnlyPayload))]
[YamlSerializable(typeof(GeneratedOptionalInitOnlyPayload))]
[YamlSerializable(typeof(GeneratedNullableInitOnlyPayload))]
[YamlSerializable(typeof(GeneratedExtensionDataDictionaryPayload))]
[YamlSerializable(typeof(GeneratedExtensionDataMappingPayload))]
[YamlSerializable(typeof(GeneratedInitOnlyExtensionDataDictionaryPayload))]
[YamlSerializable(typeof(GeneratedInitOnlyExtensionDataMappingPayload))]
[YamlSerializable(typeof(GeneratedAttributedUnmappedPayload))]
[YamlSerializable(typeof(GeneratedAttributedExtensionDataPayload))]
[YamlSerializable(typeof(GeneratedMemberConverterPayload))]
[YamlSerializable(typeof(GeneratedTypeWithConverter))]
[YamlSerializable(typeof(GeneratedYamlCtorModel))]
[YamlSerializable(typeof(GeneratedJsonCtorModel))]
[YamlSerializable(typeof(GeneratedInternalYamlCtorModel))]
[YamlSerializable(typeof(GeneratedInternalJsonCtorModel))]
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

[YamlSourceGenerationOptions(
    WriteIndented = false,
    PropertyNameCaseInsensitive = true,
    DefaultIgnoreCondition = YamlIgnoreCondition.WhenWritingNull,
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DictionaryKeyPolicy = JsonKnownNamingPolicy.CamelCase)]
[YamlSerializable(typeof(GeneratedWithDefaultOptions))]
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

[YamlSourceGenerationOptions(
    Converters = new[] { typeof(ConstantIntConverter) })]
[YamlSerializable(typeof(int))]
internal partial class TestYamlSerializerContextWithConverters : YamlSerializerContext
{
}

[YamlSourceGenerationOptions(
    Schema = YamlSchemaKind.Extended,
    UseSchema = true)]
[YamlSerializable(typeof(GeneratedSchemaAwareScalars))]
internal partial class TestYamlSerializerContextWithSchema : YamlSerializerContext
{
}

[YamlSourceGenerationOptions(
    UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow)]
[YamlSerializable(typeof(GeneratedWithDefaultOptions))]
internal partial class TestYamlSerializerContextWithStrictUnmappedMembers : YamlSerializerContext
{
}

[YamlSerializable(typeof(GeneratedPerson), TypeInfoPropertyName = "GeneratedPersonTypeInfo")]
[YamlSerializable(typeof(Dictionary<string, int>), TypeInfoPropertyName = "IntMapTypeInfo")]
internal partial class TestYamlSerializerContextWithCustomPropertyNames : YamlSerializerContext
{
}

[TestClass]
public class YamlSerializerSourceGenerationTests
{
    [TestMethod]
    public void GeneratedContext_MergeKey_AppliesToDictionaryStringKey()
    {
        var context = TestYamlSerializerContext.Default;
        var yaml = """
            <<:
              a: 1
              b: 2
            b: 3
            """;

        var dictionary = YamlSerializer.Deserialize(yaml, context.DictionaryStringInt32);

        Assert.IsNotNull(dictionary);
        Assert.AreEqual(1, dictionary["a"]);
        Assert.AreEqual(3, dictionary["b"]);
    }

    [TestMethod]
    public void GeneratedContext_MergeKey_AppliesToObject()
    {
        var context = TestYamlSerializerContext.Default;
        var yaml = """
            <<:
              first_name: Ada
              Age: 37
            Age: 38
            """;

        var person = YamlSerializer.Deserialize(yaml, context.GeneratedPerson);

        Assert.IsNotNull(person);
        Assert.AreEqual("Ada", person.FirstName);
        Assert.AreEqual(38, person.Age);
    }

    [TestMethod]
    public void GeneratedContext_MergeKey_AppliesToParameterizedConstructor()
    {
        var context = TestYamlSerializerContext.Default;
        var yaml = """
            <<:
              Name: Ada
              Age: 37
            Age: 38
            """;

        var model = YamlSerializer.Deserialize(yaml, context.GeneratedYamlCtorModel);

        Assert.IsNotNull(model);
        Assert.AreEqual("Ada", model.Name);
        Assert.AreEqual(38, model.Age);
    }

    [TestMethod]
    public void GeneratedContext_MergeKey_ExplicitKeyBeforeMergeWins()
    {
        var context = TestYamlSerializerContext.Default;
        var yaml = """
            Age: 38
            <<:
              Name: Ada
              Age: 37
            """;

        var model = YamlSerializer.Deserialize(yaml, context.GeneratedYamlCtorModel);

        Assert.IsNotNull(model);
        Assert.AreEqual("Ada", model.Name);
        Assert.AreEqual(38, model.Age);
    }

    [TestMethod]
    public void GeneratedContextProvidesTypedMetadata()
    {
        var context = new TestYamlSerializerContext();
        var typeInfo = context.GeneratedPerson;
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
    public void GeneratedContext_WellKnownScalarTypes_RoundTrip()
    {
        var payload = new GeneratedWellKnownScalars
        {
            WhenUtc = new DateTime(2026, 03, 01, 12, 34, 56, DateTimeKind.Utc),
            WhenOffset = new DateTimeOffset(2026, 03, 01, 12, 34, 56, TimeSpan.FromHours(2)),
            Id = Guid.Parse("6d0c86e2-1e37-4c33-9c2f-5304a33f2c5e"),
            Duration = TimeSpan.FromMilliseconds(1234),
        };

        var context = TestYamlSerializerContext.Default;
        var typeInfo = context.GeneratedWellKnownScalars;

        var yaml = YamlSerializer.Serialize(payload, typeInfo);
        var roundTrip = YamlSerializer.Deserialize(yaml, typeInfo);

        Assert.IsNotNull(roundTrip);
        Assert.AreEqual(payload.WhenUtc, roundTrip.WhenUtc);
        Assert.AreEqual(payload.WhenOffset, roundTrip.WhenOffset);
        Assert.AreEqual(payload.Id, roundTrip.Id);
        Assert.AreEqual(payload.Duration, roundTrip.Duration);
    }

    [TestMethod]
    public void GeneratedContext_ModernScalarTypes_RoundTrip()
    {
        var payload = new GeneratedModernScalars
        {
            Date = new DateOnly(2026, 03, 01),
            Time = new TimeOnly(12, 34, 56),
            Ratio = (Half)1.5f,
            Big = Int128.Parse("123456789012345678901234567890"),
            UBig = UInt128.Parse("123456789012345678901234567891"),
        };

        var context = TestYamlSerializerContext.Default;
        var typeInfo = context.GeneratedModernScalars;

        var yaml = YamlSerializer.Serialize(payload, typeInfo);
        var roundTrip = YamlSerializer.Deserialize(yaml, typeInfo);

        Assert.IsNotNull(roundTrip);
        Assert.AreEqual(payload.Date, roundTrip.Date);
        Assert.AreEqual(payload.Time, roundTrip.Time);
        Assert.AreEqual(payload.Ratio, roundTrip.Ratio);
        Assert.AreEqual(payload.Big, roundTrip.Big);
        Assert.AreEqual(payload.UBig, roundTrip.UBig);
    }

    [TestMethod]
    public void GeneratedContextExposesDefaultTypeInfoPropertyNames()
    {
        var context = TestYamlSerializerContext.Default;

        Assert.IsNotNull(context.GeneratedPerson);
        Assert.IsNotNull(context.GeneratedModernScalars);
        Assert.IsNotNull(context.Boolean);
        Assert.IsNotNull(context.Int32);
        Assert.IsNotNull(context.NullableInt32);
        Assert.IsNotNull(context.ListInt32);
        Assert.IsNotNull(context.DictionaryStringInt32);
        Assert.IsNotNull(context.Int32Array);

        var yaml = YamlSerializer.Serialize(
            new GeneratedPerson
            {
                FirstName = "Ada",
                Age = 37,
            },
            context.GeneratedPerson);
        var person = YamlSerializer.Deserialize(yaml, context.GeneratedPerson);

        StringAssert.Contains(yaml, "first_name: Ada");
        Assert.IsNotNull(person);
        Assert.AreEqual("Ada", person.FirstName);
        Assert.AreEqual(37, person.Age);
    }

    [TestMethod]
    public void GeneratedContextSupportsCustomTypeInfoPropertyNames()
    {
        var context = TestYamlSerializerContextWithCustomPropertyNames.Default;

        Assert.IsNotNull(context.GeneratedPersonTypeInfo);
        Assert.IsNotNull(context.IntMapTypeInfo);

        var yaml = YamlSerializer.Serialize(
            new GeneratedPerson
            {
                FirstName = "Ada",
                Age = 37,
            },
            context.GeneratedPersonTypeInfo);
        var person = YamlSerializer.Deserialize(yaml, context.GeneratedPersonTypeInfo);
        var resolved = context.GetTypeInfo(typeof(GeneratedPerson), context.GeneratedPersonTypeInfo.Options);

        StringAssert.Contains(yaml, "first_name: Ada");
        Assert.IsNotNull(person);
        Assert.AreEqual("Ada", person.FirstName);
        Assert.AreEqual(37, person.Age);
        Assert.AreSame(context.GeneratedPersonTypeInfo, resolved);
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
            context);
        var container = (GeneratedContainer?)YamlSerializer.Deserialize(yaml, typeof(GeneratedContainer), context);

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
    public void GenericSerializerUsesTypeInfoResolverFromContextOptions()
    {
        var context = TestYamlSerializerContext.Default;
        var options = context.GeneratedPerson.Options;

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
    public void GeneratedContextDefaultAppliesYamlSourceGenerationOptions()
    {
        var context = TestYamlSerializerContextWithOptions.Default;
        var options = context.GeneratedWithDefaultOptions.Options;

        Assert.IsFalse(options.WriteIndented);
        Assert.IsTrue(options.PropertyNameCaseInsensitive);
        Assert.AreEqual(YamlIgnoreCondition.WhenWritingNull, options.DefaultIgnoreCondition);
        Assert.AreSame(JsonNamingPolicy.CamelCase, options.PropertyNamingPolicy);
        Assert.AreSame(JsonNamingPolicy.CamelCase, options.DictionaryKeyPolicy);
        Assert.AreEqual(JsonUnmappedMemberHandling.Skip, options.UnmappedMemberHandling);
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
    public void GeneratedContextThrowsWhenOptionsInstanceDoesNotMatchContext()
    {
        var context = TestYamlSerializerContext.Default;
        var options = new YamlSerializerOptions
        {
            TypeInfoResolver = context,
        };

        _ = Assert.Throws<InvalidOperationException>(() => YamlSerializer.Serialize(new GeneratedPerson(), options));
    }

    [TestMethod]
    public void GeneratedContext_CanUseSchemaAwareScalarResolution()
    {
        var context = TestYamlSerializerContextWithSchema.Default;
        var options = context.GeneratedSchemaAwareScalars.Options;

        Assert.IsTrue(options.UseSchema);
        Assert.AreEqual(YamlSchemaKind.Extended, options.Schema);

        var yaml = """
            NullableText: null
            QuotedText: "null"
            PlainFlag: yes
            QuotedFlag: "yes"
            """;
        var value = YamlSerializer.Deserialize(yaml, context.GeneratedSchemaAwareScalars);

        Assert.IsNotNull(value);
        Assert.IsNull(value.NullableText);
        Assert.AreEqual("null", value.QuotedText);
        Assert.AreEqual(true, value.PlainFlag);
        Assert.AreEqual("yes", value.QuotedFlag);
    }

    [TestMethod]
    public void GeneratedContext_SkipsUnmappedMembersByDefault()
    {
        var context = TestYamlSerializerContext.Default;
        var value = YamlSerializer.Deserialize(
            "first_name: Ada\nAge: 37\nUnknown: test\n",
            context.GeneratedPerson);

        Assert.IsNotNull(value);
        Assert.AreEqual("Ada", value.FirstName);
        Assert.AreEqual(37, value.Age);
    }

    [TestMethod]
    public void GeneratedContext_CanDisallowUnmappedMembersViaOptions()
    {
        var context = TestYamlSerializerContextWithStrictUnmappedMembers.Default;
        var options = context.GeneratedWithDefaultOptions.Options;

        Assert.AreEqual(JsonUnmappedMemberHandling.Disallow, options.UnmappedMemberHandling);

        var exception = Assert.Throws<YamlException>(() => YamlSerializer.Deserialize(
            "DisplayName: Ada\nUnknown: test\n",
            context.GeneratedWithDefaultOptions));

        StringAssert.Contains(exception.Message, "Unknown");
        StringAssert.Contains(exception.Message, typeof(GeneratedWithDefaultOptions).ToString());
    }

    [TestMethod]
    public void GeneratedContext_HonorsJsonUnmappedMemberHandlingAttribute()
    {
        var context = TestYamlSerializerContext.Default;

        var exception = Assert.Throws<YamlException>(() => YamlSerializer.Deserialize(
            "DisplayName: Ada\nUnknown: test\n",
            context.GeneratedAttributedUnmappedPayload));

        StringAssert.Contains(exception.Message, "Unknown");
        StringAssert.Contains(exception.Message, typeof(GeneratedAttributedUnmappedPayload).ToString());
    }

    [TestMethod]
    public void GeneratedContext_UnmappedMemberHandlingDoesNotConflictWithExtensionData()
    {
        var context = TestYamlSerializerContext.Default;
        var value = YamlSerializer.Deserialize(
            "DisplayName: Ada\nUnknown: test\n",
            context.GeneratedAttributedExtensionDataPayload);

        Assert.IsNotNull(value);
        Assert.AreEqual("Ada", value.DisplayName);
        Assert.AreEqual("test", value.Extra["Unknown"]);
    }

    [TestMethod]
    public void GeneratedContextOptionsCanRegisterConvertersAtBuildTime()
    {
        var context = TestYamlSerializerContextWithConverters.Default;
        var options = context.Int32.Options;
        Assert.AreEqual(1, options.Converters.Count);
        Assert.IsInstanceOfType(options.Converters[0], typeof(ConstantIntConverter));

        var yaml = YamlSerializer.Serialize(42, typeof(int), context);
        Assert.AreEqual("123\n", yaml);

        var roundTrip = YamlSerializer.Deserialize(yaml, typeof(int), context);
        Assert.AreEqual(123, roundTrip);
    }

    [TestMethod]
    public void GeneratedContextRoundTripsPrimitiveMembers()
    {
        var context = new TestYamlSerializerContext();
        var typeInfo = context.GeneratedPrimitives;

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

        var enumTypeInfo = context.GeneratedColor;
        var yamlEnum = YamlSerializer.Serialize(GeneratedColor.Red, enumTypeInfo);
        Assert.AreEqual("Red\n", yamlEnum);
        Assert.AreEqual(GeneratedColor.Red, YamlSerializer.Deserialize(yamlEnum, enumTypeInfo));

        var nullableTypeInfo = context.NullableInt32;
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
        var typeInfo = context.GeneratedCollections;

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

        var listTypeInfo = context.ListInt32;
        var yamlList = YamlSerializer.Serialize(new List<int> { 1, 2, 3 }, listTypeInfo);
        var list = YamlSerializer.Deserialize(yamlList, listTypeInfo);
        Assert.IsNotNull(list);
        CollectionAssert.AreEqual(new[] { 1, 2, 3 }, list);

        var arrayTypeInfo = context.Int32Array;
        var yamlArray = YamlSerializer.Serialize(new[] { 4, 5 }, arrayTypeInfo);
        CollectionAssert.AreEqual(new[] { 4, 5 }, YamlSerializer.Deserialize(yamlArray, arrayTypeInfo));

        var dictTypeInfo = context.DictionaryStringInt32;
        var yamlDict = YamlSerializer.Serialize(new Dictionary<string, int> { ["a"] = 1, ["b"] = 2 }, dictTypeInfo);
        var dict = YamlSerializer.Deserialize(yamlDict, dictTypeInfo);
        Assert.IsNotNull(dict);
        Assert.AreEqual(1, dict["a"]);
        Assert.AreEqual(2, dict["b"]);
    }

    [TestMethod]
    public void GeneratedContextSupportsAdditionalRootCollections()
    {
        var context = new TestYamlSerializerContext();

        var readOnlyListTypeInfo = context.IReadOnlyListInt32;
        var yamlReadOnly = YamlSerializer.Serialize((IReadOnlyList<int>)new List<int> { 1, 2 }, readOnlyListTypeInfo);
        var roundReadOnly = YamlSerializer.Deserialize(yamlReadOnly, readOnlyListTypeInfo);
        Assert.IsNotNull(roundReadOnly);
        Assert.AreEqual(2, roundReadOnly.Count);
        Assert.AreEqual(1, roundReadOnly[0]);

        var setTypeInfo = context.HashSetInt32;
        var yamlSet = YamlSerializer.Serialize(new HashSet<int> { 1, 2, 1 }, setTypeInfo);
        var roundSet = YamlSerializer.Deserialize(yamlSet, setTypeInfo);
        Assert.IsNotNull(roundSet);
        Assert.AreEqual(2, roundSet.Count);

        var dictTypeInfo = context.DictionaryInt32Int32;
        var yamlDict = YamlSerializer.Serialize(new Dictionary<int, int> { [1] = 2 }, dictTypeInfo);
        StringAssert.Contains(yamlDict, "1:");
        var roundDict = YamlSerializer.Deserialize(yamlDict, dictTypeInfo);
        Assert.IsNotNull(roundDict);
        Assert.AreEqual(2, roundDict[1]);

        var enumDictTypeInfo = context.IReadOnlyDictionaryGeneratedColorInt32;
        var yamlEnumDict = YamlSerializer.Serialize((IReadOnlyDictionary<GeneratedColor, int>)new Dictionary<GeneratedColor, int> { [GeneratedColor.Red] = 1 }, enumDictTypeInfo);
        StringAssert.Contains(yamlEnumDict, "Red:");
        var roundEnumDict = YamlSerializer.Deserialize(yamlEnumDict, enumDictTypeInfo);
        Assert.IsNotNull(roundEnumDict);
        Assert.AreEqual(1, roundEnumDict[GeneratedColor.Red]);

        var immutableArrayTypeInfo = context.ImmutableArrayInt32;
        var yamlImmutable = YamlSerializer.Serialize(ImmutableArray.Create(3, 4), immutableArrayTypeInfo);
        var roundImmutable = YamlSerializer.Deserialize(yamlImmutable, immutableArrayTypeInfo);
        Assert.AreEqual(2, roundImmutable.Length);
        Assert.AreEqual(3, roundImmutable[0]);
        Assert.AreEqual(4, roundImmutable[1]);
    }

    [TestMethod]
    public void GeneratedContextRoundTripsAdditionalCollectionMembers()
    {
        var context = new TestYamlSerializerContext();
        var typeInfo = context.GeneratedMoreCollections;

        var value = new GeneratedMoreCollections
        {
            ReadOnlyNumbers = new List<int> { 1, 2 },
            Tags = new HashSet<string> { "a", "b", "a" },
            IntKeyMap = new Dictionary<int, string> { [1] = "one", [2] = "two" },
            EnumKeyMap = new Dictionary<GeneratedColor, int> { [GeneratedColor.Green] = 2 },
            ImmutableNumbers = ImmutableArray.Create(10, 20),
            ImmutableNames = ImmutableList.Create("Ada", "Bob"),
        };

        var yaml = YamlSerializer.Serialize(value, typeInfo);
        var roundtripped = YamlSerializer.Deserialize(yaml, typeInfo);

        Assert.IsNotNull(roundtripped);
        Assert.IsNotNull(roundtripped.ReadOnlyNumbers);
        Assert.AreEqual(2, roundtripped.ReadOnlyNumbers.Count);
        Assert.AreEqual(2, roundtripped.ReadOnlyNumbers[1]);

        Assert.IsNotNull(roundtripped.Tags);
        Assert.AreEqual(2, roundtripped.Tags.Count);
        Assert.IsTrue(roundtripped.Tags.Contains("a"));

        Assert.AreEqual("one", roundtripped.IntKeyMap[1]);
        Assert.IsNotNull(roundtripped.EnumKeyMap);
        Assert.AreEqual(2, roundtripped.EnumKeyMap[GeneratedColor.Green]);

        Assert.AreEqual(2, roundtripped.ImmutableNumbers.Length);
        Assert.AreEqual(10, roundtripped.ImmutableNumbers[0]);

        Assert.IsNotNull(roundtripped.ImmutableNames);
        Assert.AreEqual(2, roundtripped.ImmutableNames.Count);
        Assert.AreEqual("Ada", roundtripped.ImmutableNames[0]);

        StringAssert.Contains(yaml, "IntKeyMap:");
        StringAssert.Contains(yaml, "1:");
        StringAssert.Contains(yaml, "EnumKeyMap:");
        StringAssert.Contains(yaml, "Green:");
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

        var primitivesTypeInfo = context.GeneratedPrimitives;
        var yaml = YamlSerializer.Serialize(new GeneratedPrimitives { Int32Value = 5 }, primitivesTypeInfo);
        StringAssert.Contains(yaml, "Int32Value: 123");

        var roundtripped = YamlSerializer.Deserialize(yaml, primitivesTypeInfo);
        Assert.IsNotNull(roundtripped);
        Assert.AreEqual(123, roundtripped.Int32Value);

        var listTypeInfo = context.ListInt32;
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

        var typeInfo = context.GeneratedReferenceContainer;
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

        var typeInfo = context.GeneratedReferenceNode;
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

        var typeInfo = context.GeneratedZoo;
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

        var typeInfo = context.GeneratedTaggedZoo;
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
    public void GeneratedContextSupportsPolymorphism_DefaultDerivedType_MissingDiscriminator()
    {
        var context = new TestYamlSerializerContext();

        var typeInfo = context.GeneratedDefaultZoo;
        var yaml = "Animal:\n  Name: Cupcake\n";

        var roundtripped = YamlSerializer.Deserialize(yaml, typeInfo);
        Assert.IsNotNull(roundtripped);
        Assert.IsInstanceOfType(roundtripped.Animal, typeof(GeneratedDefaultOther));
        Assert.AreEqual("Cupcake", roundtripped.Animal!.Name);
    }

    [TestMethod]
    public void GeneratedContextSupportsPolymorphism_DefaultDerivedType_MatchedDiscriminator()
    {
        var context = new TestYamlSerializerContext();

        var typeInfo = context.GeneratedDefaultZoo;
        var yaml = "Animal:\n  type: cat\n  Name: Biscuit\n  Lives: 7\n";

        var roundtripped = YamlSerializer.Deserialize(yaml, typeInfo);
        Assert.IsNotNull(roundtripped);
        Assert.IsInstanceOfType(roundtripped.Animal, typeof(GeneratedDefaultCat));
        var cat = (GeneratedDefaultCat)roundtripped.Animal!;
        Assert.AreEqual("Biscuit", cat.Name);
        Assert.AreEqual(7, cat.Lives);
    }

    [TestMethod]
    public void GeneratedContextSupportsPolymorphism_DefaultDerivedType_UnknownDiscriminator()
    {
        var context = new TestYamlSerializerContext();

        var typeInfo = context.GeneratedDefaultZoo;
        var yaml = "Animal:\n  type: lizard\n  Name: Gex\n";

        var roundtripped = YamlSerializer.Deserialize(yaml, typeInfo);
        Assert.IsNotNull(roundtripped);
        Assert.IsInstanceOfType(roundtripped.Animal, typeof(GeneratedDefaultOther));
        Assert.AreEqual("Gex", roundtripped.Animal!.Name);
    }

    [TestMethod]
    public void GeneratedContextSupportsPolymorphism_DefaultDerivedType_Serialization()
    {
        var context = new TestYamlSerializerContext();

        var typeInfo = context.GeneratedDefaultZoo;
        var yaml = YamlSerializer.Serialize(
            new GeneratedDefaultZoo
            {
                Animal = new GeneratedDefaultOther { Name = "Cupcake" },
            },
            typeInfo);

        Assert.IsFalse(yaml.Contains("type:", StringComparison.Ordinal));
        StringAssert.Contains(yaml, "Name: Cupcake");

        // Cat should still get a discriminator
        var yamlCat = YamlSerializer.Serialize(
            new GeneratedDefaultZoo
            {
                Animal = new GeneratedDefaultCat { Name = "Biscuit", Lives = 7 },
            },
            typeInfo);

        StringAssert.Contains(yamlCat, "type: cat");
        StringAssert.Contains(yamlCat, "Name: Biscuit");
    }

    [TestMethod]
    public void GeneratedContextSupportsPolymorphism_YamlDefaultDerivedType_MissingDiscriminator()
    {
        var context = new TestYamlSerializerContext();

        var typeInfo = context.GeneratedYamlDefaultZoo;
        var yaml = "Animal:\n  Name: Cupcake\n";

        var roundtripped = YamlSerializer.Deserialize(yaml, typeInfo);
        Assert.IsNotNull(roundtripped?.Animal);
        Assert.IsInstanceOfType(roundtripped.Animal, typeof(GeneratedYamlDefaultOther));
        Assert.AreEqual("Cupcake", roundtripped.Animal!.Name);
    }

    [TestMethod]
    public void GeneratedContextSupportsPolymorphism_YamlDefaultDerivedType_MatchedDiscriminator()
    {
        var context = new TestYamlSerializerContext();

        var typeInfo = context.GeneratedYamlDefaultZoo;
        var yaml = "Animal:\n  $type: cat\n  Name: Biscuit\n  Lives: 7\n";

        var roundtripped = YamlSerializer.Deserialize(yaml, typeInfo);
        Assert.IsNotNull(roundtripped?.Animal);
        Assert.IsInstanceOfType(roundtripped.Animal, typeof(GeneratedYamlDefaultCat));
        var cat = (GeneratedYamlDefaultCat)roundtripped.Animal!;
        Assert.AreEqual("Biscuit", cat.Name);
        Assert.AreEqual(7, cat.Lives);
    }

    [TestMethod]
    public void GeneratedContextSupportsPolymorphism_YamlDefaultDerivedType_Serialization()
    {
        var context = new TestYamlSerializerContext();

        var typeInfo = context.GeneratedYamlDefaultZoo;
        var yaml = YamlSerializer.Serialize(
            new GeneratedYamlDefaultZoo
            {
                Animal = new GeneratedYamlDefaultOther { Name = "Cupcake" },
            },
            typeInfo);

        Assert.IsFalse(yaml.Contains("$type:", StringComparison.Ordinal));
        StringAssert.Contains(yaml, "Name: Cupcake");

        // Cat should still get a discriminator
        var yamlCat = YamlSerializer.Serialize(
            new GeneratedYamlDefaultZoo
            {
                Animal = new GeneratedYamlDefaultCat { Name = "Biscuit", Lives = 7 },
            },
            typeInfo);

        StringAssert.Contains(yamlCat, "$type: cat");
        StringAssert.Contains(yamlCat, "Name: Biscuit");
    }

    [TestMethod]
    public void GeneratedContextSupportsPolymorphism_YamlUnknownHandlingFallBackToBase()
    {
        var context = new TestYamlSerializerContext();

        var typeInfo = context.GeneratedFallbackZoo;
        var yaml = "Shape:\n  $type: unknown\n  Name: Base\n";

        var roundtripped = YamlSerializer.Deserialize(yaml, typeInfo);
        Assert.IsNotNull(roundtripped);
        Assert.IsInstanceOfType(roundtripped.Shape, typeof(GeneratedFallbackShape));
        Assert.AreEqual("Base", roundtripped.Shape!.Name);
    }

    [TestMethod]
    public void GeneratedContextSupportsPolymorphism_JsonIntDiscriminator()
    {
        var context = new TestYamlSerializerContext();

        var zoo = new GeneratedJsonIntZoo { Animal = new GeneratedJsonIntDog { Name = "Rex", BarkVolume = 3 } };
        var yaml = YamlSerializer.Serialize(zoo, context.GeneratedJsonIntZoo);
        StringAssert.Contains(yaml, "$type: 1");

        var roundtripped = YamlSerializer.Deserialize(yaml, context.GeneratedJsonIntZoo);
        Assert.IsNotNull(roundtripped?.Animal);
        Assert.IsInstanceOfType(roundtripped.Animal, typeof(GeneratedJsonIntDog));
        Assert.AreEqual("Rex", roundtripped.Animal!.Name);
        Assert.AreEqual(3, ((GeneratedJsonIntDog)roundtripped.Animal).BarkVolume);
    }

    [TestMethod]
    public void GeneratedContextSupportsPolymorphism_YamlIntDiscriminator()
    {
        var context = new TestYamlSerializerContext();

        var zoo = new GeneratedYamlIntZoo { Animal = new GeneratedYamlIntCat { Name = "Mittens", Lives = 9 } };
        var yaml = YamlSerializer.Serialize(zoo, context.GeneratedYamlIntZoo);
        StringAssert.Contains(yaml, "$type: 2");

        var roundtripped = YamlSerializer.Deserialize(yaml, context.GeneratedYamlIntZoo);
        Assert.IsNotNull(roundtripped?.Animal);
        Assert.IsInstanceOfType(roundtripped.Animal, typeof(GeneratedYamlIntCat));
        Assert.AreEqual("Mittens", roundtripped.Animal!.Name);
        Assert.AreEqual(9, ((GeneratedYamlIntCat)roundtripped.Animal).Lives);
    }

    [TestMethod]
    public void GeneratedContextSupportsPolymorphism_JsonUnknownHandlingFallBackToBase()
    {
        var context = new TestYamlSerializerContext();

        var typeInfo = context.GeneratedJsonFallbackZoo;
        var yaml = "Shape:\n  $type: unknown\n  Name: Base\n";

        var roundtripped = YamlSerializer.Deserialize(yaml, typeInfo);
        Assert.IsNotNull(roundtripped);
        Assert.IsInstanceOfType(roundtripped.Shape, typeof(GeneratedJsonFallbackShape));
        Assert.AreEqual("Base", roundtripped.Shape!.Name);
    }

    [TestMethod]
    public void GeneratedContextErrorsIncludeSourceNameAndLocation()
    {
        var context = new TestYamlSerializerContext(
            new YamlSerializerOptions
            {
                SourceName = "generated.yaml",
            });

        var typeInfo = context.GeneratedPerson;
        var exception = Assert.Throws<YamlException>(() => YamlSerializer.Deserialize("123", typeInfo));
        Assert.AreEqual("generated.yaml", exception.SourceName);
        StringAssert.Contains(exception.Message, "Lin:");
        StringAssert.Contains(exception.Message, "Col:");
    }

    [TestMethod]
    public void GeneratedContextInvokesLifecycleCallbacks()
    {
        var context = new TestYamlSerializerContext();
        var typeInfo = context.GeneratedLifecycleCallbacks;

        var value = new GeneratedLifecycleCallbacks { Value = 7 };
        var yaml = YamlSerializer.Serialize(value, typeInfo);

        Assert.AreEqual(1, value.OnSerializingCount);
        Assert.AreEqual(1, value.OnSerializedCount);
        StringAssert.Contains(yaml, "Value: 7");

        var roundtripped = YamlSerializer.Deserialize(yaml, typeInfo);
        Assert.IsNotNull(roundtripped);
        Assert.AreEqual(1, roundtripped.OnDeserializingCount);
        Assert.AreEqual(1, roundtripped.OnDeserializedCount);
        Assert.AreEqual(7, roundtripped.Value);
    }

    [TestMethod]
    public void GeneratedContextHonorsYamlRequiredAttribute()
    {
        var context = new TestYamlSerializerContext();
        var typeInfo = context.GeneratedRequiredPayload;

        var exception = Assert.Throws<YamlException>(() => YamlSerializer.Deserialize("Other: 1", typeInfo));
        StringAssert.Contains(exception.Message, "RequiredValue");
    }

    [TestMethod]
    public void GeneratedContextSupportsInitOnlyMembers()
    {
        var context = new TestYamlSerializerContext();
        var typeInfo = context.GeneratedOptionalInitOnlyPayload;

        var value = YamlSerializer.Deserialize("Name: Ada\nAge: 37\n", typeInfo);

        Assert.IsNotNull(value);
        Assert.AreEqual("Ada", value.Name);
        Assert.AreEqual(37, value.Age);
    }

    [TestMethod]
    public void GeneratedContextPreservesDefaultsForMissingInitOnlyMembers()
    {
        var context = new TestYamlSerializerContext();
        var typeInfo = context.GeneratedOptionalInitOnlyPayload;

        var value = YamlSerializer.Deserialize("Name: Ada\n", typeInfo);

        Assert.IsNotNull(value);
        Assert.AreEqual("Ada", value.Name);
        Assert.AreEqual(7, value.Age);
    }

    [TestMethod]
    public void GeneratedContextPreservesNullableDefaultsForMissingInitOnlyMembers()
    {
        var context = new TestYamlSerializerContext();
        var typeInfo = context.GeneratedNullableInitOnlyPayload;

        var value = YamlSerializer.Deserialize("NonNullableName: Ada\n", typeInfo);

        Assert.IsNotNull(value);
        Assert.IsNull(value.NullableName);
        Assert.AreEqual("Ada", value.NonNullableName);
    }

    [TestMethod]
    public void GeneratedContextCanDeserializeNullableInitOnlyMembersViaResolverOverload()
    {
        var context = new TestYamlSerializerContext();

        var value = (GeneratedNullableInitOnlyPayload?)YamlSerializer.Deserialize(
            "{}\n",
            typeof(GeneratedNullableInitOnlyPayload),
            context);

        Assert.IsNotNull(value);
        Assert.IsNull(value.NullableName);
        Assert.AreEqual("fallback", value.NonNullableName);
    }

    [TestMethod]
    public void GeneratedContextHonorsYamlRequiredAttributeOnInitOnlyMembers()
    {
        var context = new TestYamlSerializerContext();
        var typeInfo = context.GeneratedYamlRequiredInitOnlyPayload;

        var exception = Assert.Throws<YamlException>(() => YamlSerializer.Deserialize("Age: 37\n", typeInfo));
        StringAssert.Contains(exception.Message, "Name");

        var value = YamlSerializer.Deserialize("Name: Ada\nAge: 37\n", typeInfo);
        Assert.IsNotNull(value);
        Assert.AreEqual("Ada", value.Name);
        Assert.AreEqual(37, value.Age);
    }

    [TestMethod]
    public void GeneratedContextHonorsJsonRequiredAttributeOnInitOnlyMembers()
    {
        var context = new TestYamlSerializerContext();
        var typeInfo = context.GeneratedJsonRequiredInitOnlyPayload;

        var exception = Assert.Throws<YamlException>(() => YamlSerializer.Deserialize("Age: 37\n", typeInfo));
        StringAssert.Contains(exception.Message, "Name");

        var value = YamlSerializer.Deserialize("Name: Ada\nAge: 37\n", typeInfo);
        Assert.IsNotNull(value);
        Assert.AreEqual("Ada", value.Name);
        Assert.AreEqual(37, value.Age);
    }

    [TestMethod]
    public void GeneratedContextSupportsYamlExtensionDataDictionary()
    {
        var context = new TestYamlSerializerContext();
        var typeInfo = context.GeneratedExtensionDataDictionaryPayload;

        var yaml = """
Known: 2
extra_int: 1
extra_list:
  - a
  - b
""";

        var roundtripped = YamlSerializer.Deserialize(yaml, typeInfo);
        Assert.IsNotNull(roundtripped);
        Assert.AreEqual(2, roundtripped.Known);
        Assert.IsNotNull(roundtripped.Extra);
        Assert.IsTrue(roundtripped.Extra.ContainsKey("extra_int"));
        Assert.IsTrue(roundtripped.Extra.ContainsKey("extra_list"));

        var serialized = YamlSerializer.Serialize(
            new GeneratedExtensionDataDictionaryPayload
            {
                Known = 3,
                Extra = new Dictionary<string, object?> { ["x"] = 5 },
            },
            typeInfo);
        StringAssert.Contains(serialized, "Known: 3");
        StringAssert.Contains(serialized, "x: 5");
    }

    [TestMethod]
    public void GeneratedContextSupportsYamlExtensionDataMapping()
    {
        var context = new TestYamlSerializerContext();
        var typeInfo = context.GeneratedExtensionDataMappingPayload;

        var roundtripped = YamlSerializer.Deserialize("a: 1", typeInfo);
        Assert.IsNotNull(roundtripped);
        Assert.IsNotNull(roundtripped.Extra);
        Assert.AreEqual(1, roundtripped.Extra.Count);

        var serialized = YamlSerializer.Serialize(
            new GeneratedExtensionDataMappingPayload
            {
                Extra = new SharpYaml.Model.YamlMapping
                {
                    { new SharpYaml.Model.YamlValue("x"), new SharpYaml.Model.YamlValue("y") },
                },
            },
            typeInfo);
        StringAssert.Contains(serialized, "x:");
        StringAssert.Contains(serialized, "y");
    }

    [TestMethod]
    public void GeneratedContextSupportsInitOnlyYamlExtensionDataDictionary()
    {
        var context = new TestYamlSerializerContext();
        var typeInfo = context.GeneratedInitOnlyExtensionDataDictionaryPayload;

        var yaml = """
Known: 2
extra_int: 1
extra_list:
  - a
  - b
""";

        var roundtripped = YamlSerializer.Deserialize(yaml, typeInfo);
        Assert.IsNotNull(roundtripped);
        Assert.AreEqual(2, roundtripped.Known);
        Assert.IsNotNull(roundtripped.Extra);
        Assert.IsTrue(roundtripped.Extra.ContainsKey("extra_int"));
        Assert.IsTrue(roundtripped.Extra.ContainsKey("extra_list"));

        var withoutExtras = YamlSerializer.Deserialize("Known: 3\n", typeInfo);
        Assert.IsNotNull(withoutExtras);
        Assert.IsNotNull(withoutExtras.Extra);
        Assert.AreEqual(0, withoutExtras.Extra.Count);

        var serialized = YamlSerializer.Serialize(
            new GeneratedInitOnlyExtensionDataDictionaryPayload
            {
                Known = 4,
                Extra = new Dictionary<string, object?> { ["x"] = 5 },
            },
            typeInfo);
        StringAssert.Contains(serialized, "Known: 4");
        StringAssert.Contains(serialized, "x: 5");
    }

    [TestMethod]
    public void GeneratedContextSupportsInitOnlyYamlExtensionDataMapping()
    {
        var context = new TestYamlSerializerContext();
        var typeInfo = context.GeneratedInitOnlyExtensionDataMappingPayload;

        var roundtripped = YamlSerializer.Deserialize("Known: 2\na: 1\n", typeInfo);
        Assert.IsNotNull(roundtripped);
        Assert.AreEqual(2, roundtripped.Known);
        Assert.IsNotNull(roundtripped.Extra);
        Assert.AreEqual(1, roundtripped.Extra.Count);

        var withoutExtras = YamlSerializer.Deserialize("Known: 3\n", typeInfo);
        Assert.IsNotNull(withoutExtras);
        Assert.IsNotNull(withoutExtras.Extra);
        Assert.AreEqual(0, withoutExtras.Extra.Count);

        var serialized = YamlSerializer.Serialize(
            new GeneratedInitOnlyExtensionDataMappingPayload
            {
                Known = 4,
                Extra = new SharpYaml.Model.YamlMapping
                {
                    { new SharpYaml.Model.YamlValue("x"), new SharpYaml.Model.YamlValue("y") },
                },
            },
            typeInfo);
        StringAssert.Contains(serialized, "Known: 4");
        StringAssert.Contains(serialized, "x:");
        StringAssert.Contains(serialized, "y");
    }

    [TestMethod]
    public void GeneratedContextHonorsYamlConverterAttributeOnMember()
    {
        var context = new TestYamlSerializerContext();
        var typeInfo = context.GeneratedMemberConverterPayload;

        var yaml = YamlSerializer.Serialize(new GeneratedMemberConverterPayload { Value = 5 }, typeInfo);
        StringAssert.Contains(yaml, "Value: 123");

        var roundtripped = YamlSerializer.Deserialize("Value: 999", typeInfo);
        Assert.IsNotNull(roundtripped);
        Assert.AreEqual(123, roundtripped.Value);
    }

    [TestMethod]
    public void GeneratedContextHonorsYamlConverterAttributeOnType()
    {
        var context = new TestYamlSerializerContext();
        var typeInfo = context.GeneratedTypeWithConverter;

        var yaml = YamlSerializer.Serialize(new GeneratedTypeWithConverter { Value = 5 }, typeInfo);
        Assert.IsFalse(yaml.Contains("Value:", StringComparison.Ordinal));
        StringAssert.Contains(yaml, "5");

        var roundtripped = YamlSerializer.Deserialize("42", typeInfo);
        Assert.IsNotNull(roundtripped);
        Assert.AreEqual(42, roundtripped.Value);
    }

    [TestMethod]
    public void GeneratedContextUsesYamlConstructor()
    {
        var context = new TestYamlSerializerContext();
        var typeInfo = context.GeneratedYamlCtorModel;

        var value = YamlSerializer.Deserialize("Name: Bob\nAge: 42\n", typeInfo);

        Assert.IsNotNull(value);
        Assert.AreEqual("Bob", value.Name);
        Assert.AreEqual(42, value.Age);
    }

    [TestMethod]
    public void GeneratedContextUsesJsonConstructor()
    {
        var context = new TestYamlSerializerContext();
        var typeInfo = context.GeneratedJsonCtorModel;

        var value = YamlSerializer.Deserialize("Name: Bob\nAge: 42\n", typeInfo);

        Assert.IsNotNull(value);
        Assert.AreEqual("Bob", value.Name);
        Assert.AreEqual(42, value.Age);
    }

    [TestMethod]
    public void GeneratedContextUsesInternalYamlConstructor()
    {
        var context = new TestYamlSerializerContext();
        var typeInfo = context.GeneratedInternalYamlCtorModel;

        var value = YamlSerializer.Deserialize("Name: Bob\nAge: 42\n", typeInfo);

        Assert.IsNotNull(value);
        Assert.AreEqual("Bob", value.Name);
        Assert.AreEqual(42, value.Age);
    }

    [TestMethod]
    public void GeneratedContextUsesInternalJsonConstructor()
    {
        var context = new TestYamlSerializerContext();
        var typeInfo = context.GeneratedInternalJsonCtorModel;

        var value = YamlSerializer.Deserialize("Name: Bob\nAge: 42\n", typeInfo);

        Assert.IsNotNull(value);
        Assert.AreEqual("Bob", value.Name);
        Assert.AreEqual(42, value.Age);
    }

    [TestMethod]
    public void GeneratedContextThrowsWhenConstructorParameterMissing()
    {
        var context = new TestYamlSerializerContext();
        var typeInfo = context.GeneratedYamlCtorModel;

        var ex = Assert.Throws<YamlException>(() => YamlSerializer.Deserialize("Name: Bob\n", typeInfo));
        StringAssert.Contains(ex.Message, "age");
    }
}
