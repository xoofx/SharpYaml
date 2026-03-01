#nullable enable

using System;
using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SharpYaml.Serialization;

namespace SharpYaml.Tests.Serialization;

[TestClass]
public sealed class YamlSerializerOptionsImmutabilityTests
{
    private sealed class NoopInt32Converter : YamlConverter<int>
    {
        public override int Read(YamlReader reader)
        {
            reader.Skip();
            return 0;
        }

        public override void Write(YamlWriter writer, int value)
        {
            writer.WriteScalar(value);
        }
    }

    [TestMethod]
    public void Converters_AreCopiedFromInitializer()
    {
        var list = new List<YamlConverter>
        {
            new NoopInt32Converter(),
        };

        var options = new SharpYaml.YamlSerializerOptions
        {
            Converters = list,
        };

        list.Add(new NoopInt32Converter());

        Assert.AreEqual(1, options.Converters.Count);
    }

    [TestMethod]
    public void Context_RejectsOptionsWithDifferentTypeInfoResolver()
    {
        var resolver = new DummyResolver();
        var options = new SharpYaml.YamlSerializerOptions
        {
            TypeInfoResolver = resolver,
        };

        _ = Assert.Throws<ArgumentException>(() => new DummyContext(options));
    }

    private sealed class DummyResolver : SharpYaml.IYamlTypeInfoResolver
    {
        public SharpYaml.YamlTypeInfo? GetTypeInfo(System.Type type, SharpYaml.YamlSerializerOptions options) => null;
    }

    private sealed class DummyContext : YamlSerializerContext
    {
        public DummyContext(SharpYaml.YamlSerializerOptions options) : base(options)
        {
        }

        public override SharpYaml.YamlTypeInfo? GetTypeInfo(System.Type type, SharpYaml.YamlSerializerOptions options) => null;
    }
}
