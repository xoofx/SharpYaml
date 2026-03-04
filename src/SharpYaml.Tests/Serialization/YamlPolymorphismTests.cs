#nullable enable

using System;
using System.Text.Json.Serialization;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SharpYaml.Serialization;

namespace SharpYaml.Tests.Serialization;

[TestClass]
public class YamlPolymorphismTests
{
    [YamlPolymorphic]
    [YamlDerivedType(typeof(Dog), "dog", Tag = "!dog")]
    [YamlDerivedType(typeof(Cat), "cat", Tag = "!cat")]
    private abstract class Animal
    {
        public string Name { get; set; } = string.Empty;
    }

    private sealed class Dog : Animal
    {
        public int BarkVolume { get; set; }
    }

    private sealed class Cat : Animal
    {
        public int Lives { get; set; }
    }

    [JsonPolymorphic(TypeDiscriminatorPropertyName = "$kind")]
    [JsonDerivedType(typeof(JsonDog), "dog")]
    private abstract class JsonAnimal
    {
        public string Name { get; set; } = string.Empty;
    }

    private sealed class JsonDog : JsonAnimal
    {
        public int BarkVolume { get; set; }
    }

    [JsonPolymorphic(TypeDiscriminatorPropertyName = "type")]
    [JsonDerivedType(typeof(JsonDefaultCat), "cat")]
    [JsonDerivedType(typeof(JsonDefaultOther))]
    private abstract class JsonDefaultAnimal
    {
        public string Name { get; set; } = string.Empty;
    }

    private sealed class JsonDefaultCat : JsonDefaultAnimal
    {
        public int Lives { get; set; }
    }

    private sealed class JsonDefaultOther : JsonDefaultAnimal
    {
    }

    [YamlPolymorphic]
    [YamlDerivedType(typeof(YamlDefaultDog), "dog")]
    [YamlDerivedType(typeof(YamlDefaultOther))]
    private abstract class YamlDefaultAnimal
    {
        public string Name { get; set; } = string.Empty;
    }

    private sealed class YamlDefaultDog : YamlDefaultAnimal
    {
        public int BarkVolume { get; set; }
    }

    private sealed class YamlDefaultOther : YamlDefaultAnimal
    {
    }

    [YamlPolymorphic]
    [YamlDerivedType(typeof(Circle), "circle")]
    private class Shape
    {
        public string Name { get; set; } = string.Empty;
    }

    private sealed class Circle : Shape
    {
        public double Radius { get; set; }
    }

    [YamlPolymorphic(UnknownDerivedTypeHandling = YamlUnknownDerivedTypeHandling.FallBackToBase)]
    [YamlDerivedType(typeof(YamlFallbackCircle), "circle")]
    private class YamlFallbackShape
    {
        public string Name { get; set; } = string.Empty;
    }

    private sealed class YamlFallbackCircle : YamlFallbackShape
    {
        public double Radius { get; set; }
    }

    [JsonPolymorphic(TypeDiscriminatorPropertyName = "$type")]
    [JsonDerivedType(typeof(JsonOverriddenCircle), "circle")]
    [YamlPolymorphic(UnknownDerivedTypeHandling = YamlUnknownDerivedTypeHandling.FallBackToBase)]
    private class JsonOverriddenShape
    {
        public string Name { get; set; } = string.Empty;
    }

    private sealed class JsonOverriddenCircle : JsonOverriddenShape
    {
        public double Radius { get; set; }
    }

    [TestMethod]
    public void SerializeEmitsPropertyDiscriminatorFirst()
    {
        Animal animal = new Dog { Name = "Rex", BarkVolume = 3 };
        var yaml = SharpYaml.YamlSerializer.Serialize(animal, typeof(Animal));

        var typeIndex = yaml.IndexOf("$type:", StringComparison.Ordinal);
        var nameIndex = yaml.IndexOf("Name:", StringComparison.Ordinal);
        Assert.IsTrue(typeIndex >= 0);
        Assert.IsTrue(nameIndex > typeIndex);
        StringAssert.Contains(yaml, "$type: dog");
        StringAssert.Contains(yaml, "BarkVolume: 3");
    }

    [TestMethod]
    public void DeserializeSelectsDerivedTypeWhenDiscriminatorNotFirst()
    {
        var yaml = "Name: Rex\nBarkVolume: 3\n$type: dog\n";
        var value = SharpYaml.YamlSerializer.Deserialize<Animal>(yaml);

        Assert.IsNotNull(value);
        Assert.IsInstanceOfType<Dog>(value);
        Assert.AreEqual("Rex", value.Name);
        Assert.AreEqual(3, ((Dog)value).BarkVolume);
    }

    [TestMethod]
    public void DeserializeSelectsDerivedTypeFromJsonPolymorphicAttributes()
    {
        var yaml = "Name: Rex\nBarkVolume: 3\n$kind: dog\n";
        var value = SharpYaml.YamlSerializer.Deserialize<JsonAnimal>(yaml);

        Assert.IsNotNull(value);
        Assert.IsInstanceOfType<JsonDog>(value);
        Assert.AreEqual("Rex", value.Name);
        Assert.AreEqual(3, ((JsonDog)value).BarkVolume);
    }

    [TestMethod]
    public void UnknownDiscriminatorFailsByDefault()
    {
        var yaml = "Name: Rex\n$type: lizard\n";
        Assert.Throws<YamlException>(() => SharpYaml.YamlSerializer.Deserialize<Animal>(yaml));
    }

    [TestMethod]
    public void UnknownDiscriminatorCanFallBackToBase()
    {
        var yaml = "Name: Base\n$type: unknown\n";
        var value = SharpYaml.YamlSerializer.Deserialize<Shape>(
            yaml,
            new SharpYaml.YamlSerializerOptions
            {
                PolymorphismOptions = new SharpYaml.YamlPolymorphismOptions
                {
                    UnknownDerivedTypeHandling = SharpYaml.YamlUnknownDerivedTypeHandling.FallBackToBase,
                },
            });

        Assert.IsNotNull(value);
        Assert.IsInstanceOfType<Shape>(value);
        Assert.AreEqual("Base", value.Name);
    }

    [TestMethod]
    public void YamlPolymorphicAttributeUnknownHandlingFallsBackToBase()
    {
        var yaml = "Name: Base\n$type: unknown\n";
        var value = SharpYaml.YamlSerializer.Deserialize<YamlFallbackShape>(yaml);

        Assert.IsNotNull(value);
        Assert.IsInstanceOfType<YamlFallbackShape>(value);
        Assert.AreEqual("Base", value.Name);
    }

    [TestMethod]
    public void YamlPolymorphicAttributeUnknownHandlingOverridesJsonAttribute()
    {
        // JsonPolymorphic defaults to FailSerialization, but YamlPolymorphic overrides to FallBackToBase
        var yaml = "Name: Base\n$type: unknown\n";
        var value = SharpYaml.YamlSerializer.Deserialize<JsonOverriddenShape>(yaml);

        Assert.IsNotNull(value);
        Assert.IsInstanceOfType<JsonOverriddenShape>(value);
        Assert.AreEqual("Base", value.Name);
    }

    [TestMethod]
    public void TagDiscriminatorCanBeUsedWhenEnabled()
    {
        var yaml = "!dog\nName: Rex\nBarkVolume: 3\n";
        var value = SharpYaml.YamlSerializer.Deserialize<Animal>(
            yaml,
            new SharpYaml.YamlSerializerOptions
            {
                PolymorphismOptions = new SharpYaml.YamlPolymorphismOptions
                {
                    DiscriminatorStyle = SharpYaml.YamlTypeDiscriminatorStyle.Both,
                },
            });

        Assert.IsNotNull(value);
        Assert.IsInstanceOfType<Dog>(value);
        Assert.AreEqual(3, ((Dog)value).BarkVolume);
    }

    [TestMethod]
    public void SerializeCanEmitTagDiscriminatorOnly()
    {
        Animal animal = new Cat { Name = "Mittens", Lives = 9 };
        var yaml = SharpYaml.YamlSerializer.Serialize(
            animal,
            typeof(Animal),
            new SharpYaml.YamlSerializerOptions
            {
                PolymorphismOptions = new SharpYaml.YamlPolymorphismOptions
                {
                    DiscriminatorStyle = SharpYaml.YamlTypeDiscriminatorStyle.Tag,
                },
            });

        StringAssert.Contains(yaml, "!cat");
        Assert.IsFalse(yaml.Contains("$type:", StringComparison.Ordinal));
        StringAssert.Contains(yaml, "Lives: 9");
    }

    [TestMethod]
    public void JsonDefaultDerivedTypeDeserializesWhenDiscriminatorIsMissing()
    {
        var yaml = "Name: Cupcake\n";
        var value = SharpYaml.YamlSerializer.Deserialize<JsonDefaultAnimal>(yaml);

        Assert.IsNotNull(value);
        Assert.IsInstanceOfType<JsonDefaultOther>(value);
        Assert.AreEqual("Cupcake", value.Name);
    }

    [TestMethod]
    public void JsonDefaultDerivedTypeDeserializesWhenDiscriminatorMatches()
    {
        var yaml = "type: cat\nName: Biscuit\nLives: 7\n";
        var value = SharpYaml.YamlSerializer.Deserialize<JsonDefaultAnimal>(yaml);

        Assert.IsNotNull(value);
        Assert.IsInstanceOfType<JsonDefaultCat>(value);
        Assert.AreEqual("Biscuit", value.Name);
        Assert.AreEqual(7, ((JsonDefaultCat)value).Lives);
    }

    [TestMethod]
    public void JsonDefaultDerivedTypeDeserializesWhenDiscriminatorIsUnknown()
    {
        var yaml = "type: lizard\nName: Gex\n";
        var value = SharpYaml.YamlSerializer.Deserialize<JsonDefaultAnimal>(yaml);

        Assert.IsNotNull(value);
        Assert.IsInstanceOfType<JsonDefaultOther>(value);
        Assert.AreEqual("Gex", value.Name);
    }

    [TestMethod]
    public void JsonDefaultDerivedTypeSerializesWithoutDiscriminator()
    {
        JsonDefaultAnimal animal = new JsonDefaultOther { Name = "Cupcake" };
        var yaml = SharpYaml.YamlSerializer.Serialize(animal, typeof(JsonDefaultAnimal));

        Assert.IsFalse(yaml.Contains("type:", StringComparison.Ordinal));
        StringAssert.Contains(yaml, "Name: Cupcake");
    }

    [TestMethod]
    public void JsonDefaultDerivedTypeSerializesWithDiscriminatorForNonDefaultType()
    {
        JsonDefaultAnimal animal = new JsonDefaultCat { Name = "Biscuit", Lives = 7 };
        var yaml = SharpYaml.YamlSerializer.Serialize(animal, typeof(JsonDefaultAnimal));

        StringAssert.Contains(yaml, "type: cat");
        StringAssert.Contains(yaml, "Name: Biscuit");
    }

    [TestMethod]
    public void YamlDefaultDerivedTypeDeserializesWhenDiscriminatorIsMissing()
    {
        var yaml = "Name: Cupcake\n";
        var value = SharpYaml.YamlSerializer.Deserialize<YamlDefaultAnimal>(yaml);

        Assert.IsNotNull(value);
        Assert.IsInstanceOfType<YamlDefaultOther>(value);
        Assert.AreEqual("Cupcake", value.Name);
    }

    [TestMethod]
    public void YamlDefaultDerivedTypeDeserializesWhenDiscriminatorMatches()
    {
        var yaml = "$type: dog\nName: Rex\nBarkVolume: 5\n";
        var value = SharpYaml.YamlSerializer.Deserialize<YamlDefaultAnimal>(yaml);

        Assert.IsNotNull(value);
        Assert.IsInstanceOfType<YamlDefaultDog>(value);
        Assert.AreEqual("Rex", value.Name);
        Assert.AreEqual(5, ((YamlDefaultDog)value).BarkVolume);
    }

    [TestMethod]
    public void YamlDefaultDerivedTypeSerializesWithoutDiscriminator()
    {
        YamlDefaultAnimal animal = new YamlDefaultOther { Name = "Cupcake" };
        var yaml = SharpYaml.YamlSerializer.Serialize(animal, typeof(YamlDefaultAnimal));

        Assert.IsFalse(yaml.Contains("$type:", StringComparison.Ordinal));
        StringAssert.Contains(yaml, "Name: Cupcake");
    }

    [TestMethod]
    public void YamlDefaultDerivedTypeSerializesWithDiscriminatorForNonDefaultType()
    {
        YamlDefaultAnimal animal = new YamlDefaultDog { Name = "Rex", BarkVolume = 5 };
        var yaml = SharpYaml.YamlSerializer.Serialize(animal, typeof(YamlDefaultAnimal));

        StringAssert.Contains(yaml, "$type: dog");
        StringAssert.Contains(yaml, "Name: Rex");
    }
}
