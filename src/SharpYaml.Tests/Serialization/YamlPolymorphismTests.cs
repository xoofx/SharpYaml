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
                PolymorphismOptions =
                {
                    UnknownDerivedTypeHandling = SharpYaml.YamlUnknownDerivedTypeHandling.FallBackToBase,
                },
            });

        Assert.IsNotNull(value);
        Assert.IsInstanceOfType<Shape>(value);
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
                PolymorphismOptions =
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
                PolymorphismOptions =
                {
                    DiscriminatorStyle = SharpYaml.YamlTypeDiscriminatorStyle.Tag,
                },
            });

        StringAssert.Contains(yaml, "!cat");
        Assert.IsFalse(yaml.Contains("$type:", StringComparison.Ordinal));
        StringAssert.Contains(yaml, "Lives: 9");
    }
}
