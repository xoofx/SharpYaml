#nullable enable

using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SharpYaml.Serialization;

namespace SharpYaml.Tests.Serialization;

[TestClass]
public class YamlUnknownTagHandlingTests
{
    // ---- Model types ----

    [YamlPolymorphic(DiscriminatorStyle = YamlTypeDiscriminatorStyle.Tag)]
    [YamlDerivedType(typeof(AttributeDog), Tag = "!dog")]
    [YamlDerivedType(typeof(AttributeCat), Tag = "!cat")]
    private class AttributeAnimal
    {
        public string Name { get; set; } = string.Empty;
    }

    private sealed class AttributeDog : AttributeAnimal
    {
        public int BarkVolume { get; set; }
    }

    private sealed class AttributeCat : AttributeAnimal
    {
        public bool Indoor { get; set; }
    }

    [YamlPolymorphic(DiscriminatorStyle = YamlTypeDiscriminatorStyle.Tag, UnknownDerivedTypeHandling = YamlUnknownDerivedTypeHandling.FallBackToBase)]
    [YamlDerivedType(typeof(FallbackDog), Tag = "!dog")]
    private class FallbackAnimal
    {
        public string Name { get; set; } = string.Empty;
    }

    private sealed class FallbackDog : FallbackAnimal
    {
        public int BarkVolume { get; set; }
    }

    private abstract class RuntimeAnimal
    {
        public string Name { get; set; } = string.Empty;
    }

    private sealed class RuntimeDog : RuntimeAnimal
    {
        public int BarkVolume { get; set; }
    }

    private sealed class RuntimeCat : RuntimeAnimal
    {
        public bool Indoor { get; set; }
    }

    // ---- Attribute-based: unknown tag with default (Fail) handling ----

    [TestMethod]
    public void UnknownTagFailsByDefaultWithAttributes()
    {
        var yaml = "!lizard\nName: Gecko\n";
        var ex = Assert.Throws<YamlException>(
            () => YamlSerializer.Deserialize<AttributeAnimal>(yaml));
        StringAssert.Contains(ex.Message, "!lizard");
    }

    [TestMethod]
    public void KnownTagWorksWithAttributes()
    {
        var yaml = "!dog\nName: Rex\nBarkVolume: 5\n";
        var value = YamlSerializer.Deserialize<AttributeAnimal>(yaml);

        Assert.IsNotNull(value);
        Assert.IsInstanceOfType<AttributeDog>(value);
        Assert.AreEqual("Rex", value.Name);
        Assert.AreEqual(5, ((AttributeDog)value).BarkVolume);
    }

    [TestMethod]
    public void NoTagDeserializesToBaseTypeWithAttributes()
    {
        var yaml = "Name: Plain\n";
        var value = YamlSerializer.Deserialize<AttributeAnimal>(yaml);

        Assert.IsNotNull(value);
        Assert.IsInstanceOfType<AttributeAnimal>(value);
        Assert.AreEqual("Plain", value.Name);
    }

    // ---- Attribute-based: FallBackToBase ----

    [TestMethod]
    public void UnknownTagFallsBackToBaseWhenConfigured()
    {
        var yaml = "!lizard\nName: Gecko\n";
        var value = YamlSerializer.Deserialize<FallbackAnimal>(yaml);

        Assert.IsNotNull(value);
        Assert.IsInstanceOfType<FallbackAnimal>(value);
        Assert.AreEqual("Gecko", value.Name);
    }

    // ---- Options-level: Fail ----

    [TestMethod]
    public void UnknownTagFailsWithOptionsLevelFail()
    {
        var options = new YamlSerializerOptions
        {
            PolymorphismOptions = new YamlPolymorphismOptions
            {
                DiscriminatorStyle = YamlTypeDiscriminatorStyle.Tag,
                UnknownDerivedTypeHandling = YamlUnknownDerivedTypeHandling.Fail,
                DerivedTypeMappings =
                {
                    [typeof(RuntimeAnimal)] = new List<YamlDerivedType>
                    {
                        new YamlDerivedType(typeof(RuntimeDog), "dog") { Tag = "!dog" },
                        new YamlDerivedType(typeof(RuntimeCat), "cat") { Tag = "!cat" },
                    }
                }
            }
        };

        var yaml = "!parrot\nName: Polly\n";
        var ex = Assert.Throws<YamlException>(
            () => YamlSerializer.Deserialize<RuntimeAnimal>(yaml, options));
        StringAssert.Contains(ex.Message, "!parrot");
    }

    [TestMethod]
    public void KnownTagWorksWithRuntime()
    {
        var options = new YamlSerializerOptions
        {
            PolymorphismOptions = new YamlPolymorphismOptions
            {
                DiscriminatorStyle = YamlTypeDiscriminatorStyle.Tag,
                DerivedTypeMappings =
                {
                    [typeof(RuntimeAnimal)] = new List<YamlDerivedType>
                    {
                        new YamlDerivedType(typeof(RuntimeDog), "dog") { Tag = "!dog" },
                    }
                }
            }
        };

        var yaml = "!dog\nName: Rex\nBarkVolume: 3\n";
        var value = YamlSerializer.Deserialize<RuntimeAnimal>(yaml, options);

        Assert.IsNotNull(value);
        Assert.IsInstanceOfType<RuntimeDog>(value);
        Assert.AreEqual("Rex", value.Name);
        Assert.AreEqual(3, ((RuntimeDog)value).BarkVolume);
    }

    // ---- Options-level: FallBackToBase ----

    private class ConcreteAnimal
    {
        public string Name { get; set; } = string.Empty;
    }

    private sealed class ConcreteDog : ConcreteAnimal
    {
        public int BarkVolume { get; set; }
    }

    [TestMethod]
    public void UnknownTagFallsBackToBaseWithOptions()
    {
        var options = new YamlSerializerOptions
        {
            PolymorphismOptions = new YamlPolymorphismOptions
            {
                DiscriminatorStyle = YamlTypeDiscriminatorStyle.Tag,
                UnknownDerivedTypeHandling = YamlUnknownDerivedTypeHandling.FallBackToBase,
                DerivedTypeMappings =
                {
                    [typeof(ConcreteAnimal)] = new List<YamlDerivedType>
                    {
                        new YamlDerivedType(typeof(ConcreteDog), "dog") { Tag = "!dog" },
                    }
                }
            }
        };

        var yaml = "!parrot\nName: Polly\n";
        var value = YamlSerializer.Deserialize<ConcreteAnimal>(yaml, options);

        Assert.IsNotNull(value);
        Assert.IsInstanceOfType<ConcreteAnimal>(value);
        Assert.AreEqual("Polly", value.Name);
    }

    // ---- Attribute-level override takes precedence over options ----

    [TestMethod]
    public void AttributeUnknownHandlingOverridesOptions()
    {
        // FallbackAnimal has FallBackToBase in attribute; options say Fail — attribute wins
        var options = new YamlSerializerOptions
        {
            PolymorphismOptions = new YamlPolymorphismOptions
            {
                UnknownDerivedTypeHandling = YamlUnknownDerivedTypeHandling.Fail,
            }
        };

        var yaml = "!lizard\nName: Gecko\n";
        var value = YamlSerializer.Deserialize<FallbackAnimal>(yaml, options);

        Assert.IsNotNull(value);
        Assert.IsInstanceOfType<FallbackAnimal>(value);
        Assert.AreEqual("Gecko", value.Name);
    }

    // ---- Dictionary of polymorphic values with unknown tags ----

    [TestMethod]
    public void UnknownTagInDictionaryValueFails()
    {
        var options = new YamlSerializerOptions
        {
            PolymorphismOptions = new YamlPolymorphismOptions
            {
                DiscriminatorStyle = YamlTypeDiscriminatorStyle.Tag,
                UnknownDerivedTypeHandling = YamlUnknownDerivedTypeHandling.Fail,
                DerivedTypeMappings =
                {
                    [typeof(RuntimeAnimal)] = new List<YamlDerivedType>
                    {
                        new YamlDerivedType(typeof(RuntimeDog), "dog") { Tag = "!dog" },
                    }
                }
            }
        };

        var yaml = "rex: !dog\n  Name: Rex\n  BarkVolume: 3\npolly: !parrot\n  Name: Polly\n";
        Assert.Throws<YamlException>(
            () => YamlSerializer.Deserialize<Dictionary<string, RuntimeAnimal>>(yaml, options));
    }

    // ---- Tag-only entries (no discriminator) with unknown tags ----

    [TestMethod]
    public void UnknownTagFailsWithTagOnlyEntries()
    {
        var options = new YamlSerializerOptions
        {
            PolymorphismOptions = new YamlPolymorphismOptions
            {
                DiscriminatorStyle = YamlTypeDiscriminatorStyle.Tag,
                UnknownDerivedTypeHandling = YamlUnknownDerivedTypeHandling.Fail,
                DerivedTypeMappings =
                {
                    [typeof(RuntimeAnimal)] = new List<YamlDerivedType>
                    {
                        new YamlDerivedType(typeof(RuntimeDog)) { Tag = "!dog" },
                        new YamlDerivedType(typeof(RuntimeCat)) { Tag = "!cat" },
                    }
                }
            }
        };

        var yaml = "!fish\nName: Nemo\n";
        var ex = Assert.Throws<YamlException>(
            () => YamlSerializer.Deserialize<RuntimeAnimal>(yaml, options));
        StringAssert.Contains(ex.Message, "!fish");
    }
}
