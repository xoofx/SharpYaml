#nullable enable

using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SharpYaml.Serialization;

namespace SharpYaml.Tests.Serialization.CrossProject.Core
{
    [YamlPolymorphic]
    internal abstract class Animal
    {
        public string Name { get; set; } = string.Empty;
    }
}

namespace SharpYaml.Tests.Serialization.CrossProject.Plugins
{
    internal sealed class Dog : Core.Animal
    {
        public string Breed { get; set; } = string.Empty;
    }

    internal sealed class Cat : Core.Animal
    {
        public bool Indoor { get; set; }
    }
}

namespace SharpYaml.Tests.Serialization.CrossProject.AttributeCore
{
    [YamlPolymorphic]
    [YamlDerivedType(typeof(AttributePlugins.BuiltInDog), "dog", Tag = "!dog")]
    internal abstract class Animal
    {
        public string Name { get; set; } = string.Empty;
    }
}

namespace SharpYaml.Tests.Serialization.CrossProject.AttributePlugins
{
    internal sealed class BuiltInDog : AttributeCore.Animal
    {
        public int BarkVolume { get; set; }
    }

    internal sealed class ConflictingDog : AttributeCore.Animal
    {
        public string Skill { get; set; } = string.Empty;
    }
}

namespace SharpYaml.Tests.Serialization
{
    internal sealed class CrossProjectZoo
    {
        public CrossProject.Core.Animal? Animal { get; set; }
    }

    internal sealed class AttributeMappedZoo
    {
        public CrossProject.AttributeCore.Animal? Animal { get; set; }
    }

    [YamlSerializable(typeof(CrossProjectZoo))]
    [YamlSerializable(typeof(AttributeMappedZoo))]
    [YamlDerivedTypeMapping(typeof(CrossProject.Core.Animal), typeof(CrossProject.Plugins.Dog), "dog", Tag = "!dog")]
    [YamlDerivedTypeMapping(typeof(CrossProject.Core.Animal), typeof(CrossProject.Plugins.Cat), "cat", Tag = "!cat")]
    [YamlDerivedTypeMapping(typeof(CrossProject.AttributeCore.Animal), typeof(CrossProject.AttributePlugins.ConflictingDog), "dog", Tag = "!conflict")]
    internal partial class CrossProjectYamlContext : YamlSerializerContext
    {
        public CrossProjectYamlContext()
        {
        }

        public CrossProjectYamlContext(YamlSerializerOptions options)
            : base(options)
        {
        }
    }

    [TestClass]
    public class YamlSourceGeneratedDerivedTypeMappingTests
    {
        [TestMethod]
        public void GeneratedContextSupportsCrossProjectPropertyDiscriminatorMappings()
        {
            var context = new CrossProjectYamlContext();
            var typeInfo = context.CrossProjectZoo;

            var yaml = YamlSerializer.Serialize(
                new CrossProjectZoo
                {
                    Animal = new CrossProject.Plugins.Dog { Name = "Rex", Breed = "Collie" },
                },
                typeInfo);

            StringAssert.Contains(yaml, "$type: dog");
            StringAssert.Contains(yaml, "Breed: Collie");

            var roundtripped = YamlSerializer.Deserialize(yaml, typeInfo);
            Assert.IsNotNull(roundtripped?.Animal);
            Assert.IsInstanceOfType<CrossProject.Plugins.Dog>(roundtripped.Animal);
            var dog = (CrossProject.Plugins.Dog)roundtripped.Animal;
            Assert.AreEqual("Rex", dog.Name);
            Assert.AreEqual("Collie", dog.Breed);
        }

        [TestMethod]
        public void GeneratedContextSupportsCrossProjectTagMappings()
        {
            var context = new CrossProjectYamlContext(
                new YamlSerializerOptions
                {
                    PolymorphismOptions = new YamlPolymorphismOptions
                    {
                        DiscriminatorStyle = YamlTypeDiscriminatorStyle.Tag,
                    },
                });

            var typeInfo = context.CrossProjectZoo;
            var yaml = YamlSerializer.Serialize(
                new CrossProjectZoo
                {
                    Animal = new CrossProject.Plugins.Cat { Name = "Mittens", Indoor = true },
                },
                typeInfo);

            StringAssert.Contains(yaml, "!cat");
            Assert.IsFalse(yaml.Contains("$type:", StringComparison.Ordinal));

            var roundtripped = YamlSerializer.Deserialize(yaml, typeInfo);
            Assert.IsNotNull(roundtripped?.Animal);
            Assert.IsInstanceOfType<CrossProject.Plugins.Cat>(roundtripped.Animal);
            var cat = (CrossProject.Plugins.Cat)roundtripped.Animal;
            Assert.AreEqual("Mittens", cat.Name);
            Assert.IsTrue(cat.Indoor);
        }

        [TestMethod]
        public void GeneratedContextAutoIncludesDerivedTypesReferencedByMappings()
        {
            var context = new CrossProjectYamlContext();
            var typeInfo = context.GetTypeInfo(typeof(CrossProject.Plugins.Dog), context.Options);

            Assert.IsNotNull(typeInfo);

            var yaml = YamlSerializer.Serialize(
                new CrossProject.Plugins.Dog { Name = "Scout", Breed = "Husky" },
                typeof(CrossProject.Plugins.Dog),
                context);

            var roundtripped = (CrossProject.Plugins.Dog?)YamlSerializer.Deserialize(
                yaml,
                typeof(CrossProject.Plugins.Dog),
                context);
            Assert.IsNotNull(roundtripped);
            Assert.AreEqual("Scout", roundtripped.Name);
            Assert.AreEqual("Husky", roundtripped.Breed);
        }

        [TestMethod]
        public void GeneratedContextKeepsAttributeMappingsAheadOfContextMappings()
        {
            var context = new CrossProjectYamlContext();
            var typeInfo = context.AttributeMappedZoo;

            var roundtripped = YamlSerializer.Deserialize(
                "Animal:\n  $type: dog\n  Name: Spot\n  BarkVolume: 5\n",
                typeInfo);

            Assert.IsNotNull(roundtripped?.Animal);
            Assert.IsInstanceOfType<CrossProject.AttributePlugins.BuiltInDog>(roundtripped.Animal);
            Assert.AreEqual(5, ((CrossProject.AttributePlugins.BuiltInDog)roundtripped.Animal).BarkVolume);

            var exception = Assert.Throws<NotSupportedException>(
                () => YamlSerializer.Serialize(
                    new AttributeMappedZoo
                    {
                        Animal = new CrossProject.AttributePlugins.ConflictingDog { Name = "Patch", Skill = "herding" },
                    },
                    typeInfo));
            StringAssert.Contains(exception.Message, typeof(CrossProject.AttributePlugins.ConflictingDog).ToString());
        }

        [TestMethod]
        public void YamlDerivedTypeMappingAttributeValidatesArgumentsAndStoresValues()
        {
            Assert.Throws<ArgumentNullException>(() => new YamlDerivedTypeMappingAttribute(null!, typeof(CrossProject.Plugins.Dog)));
            Assert.Throws<ArgumentNullException>(() => new YamlDerivedTypeMappingAttribute(typeof(CrossProject.Core.Animal), null!));
            Assert.Throws<ArgumentNullException>(() => new YamlDerivedTypeMappingAttribute(typeof(CrossProject.Core.Animal), typeof(CrossProject.Plugins.Dog), (string)null!));

            var mapping = new YamlDerivedTypeMappingAttribute(
                typeof(CrossProject.Core.Animal),
                typeof(CrossProject.Plugins.Cat),
                2)
            {
                Tag = "!cat",
            };

            Assert.AreEqual(typeof(CrossProject.Core.Animal), mapping.BaseType);
            Assert.AreEqual(typeof(CrossProject.Plugins.Cat), mapping.DerivedType);
            Assert.AreEqual("2", mapping.Discriminator);
            Assert.AreEqual("!cat", mapping.Tag);
        }
    }
}
