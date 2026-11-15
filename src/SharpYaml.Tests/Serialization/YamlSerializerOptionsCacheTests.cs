using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SharpYaml.Serialization;

namespace SharpYaml.Tests.Serialization;

/// <summary>
/// Covers what an options instance keeps of the converters it resolved: it is no part of the value of the options,
/// it does not follow a copy of them, and it does not keep a type alive.
/// </summary>
[TestClass]
public sealed class YamlSerializerOptionsCacheTests
{
    [TestMethod]
    public void CollectibleTypeUnloadsAfterTheDefaultOptionsSerializedIt()
    {
        // The default options live as long as the application: the converters they resolved must not keep the type of
        // a collectible assembly, such as one of a plugin, from being unloaded.
        var type = UseCollectibleType(YamlSerializerOptions.Default);

        Assert.IsFalse(IsAliveAfterCollections(type));
    }

    [TestMethod]
    public void CollectibleTypeUnloadsWhileTheOptionsThatSerializedItAreStillUsed()
    {
        var options = new YamlSerializerOptions { IndentSize = 4 };
        var type = UseCollectibleType(options);

        Assert.IsFalse(IsAliveAfterCollections(type));
        Assert.AreEqual("- 1\n", YamlSerializer.Serialize(new List<int> { 1 }, options));
    }

    [TestMethod]
    public void OptionsKeepTheirValueOnceTheyResolvedConverters()
    {
        // A copy made before the options resolved anything, which has resolved nothing itself.
        var options = new YamlSerializerOptions { IndentSize = 4 };
        var same = options with { };
        Assert.AreEqual(same, options);

        Assert.AreEqual("Value: 5\n", YamlSerializer.Serialize(new WritePathNested { Value = 5 }, options));

        Assert.AreEqual(same, options);
        Assert.AreEqual(same.GetHashCode(), options.GetHashCode());
        Assert.AreEqual(options, options with { });
        Assert.AreEqual(options.GetHashCode(), (options with { }).GetHashCode());
    }

    [TestMethod]
    public void CopyOfOptionsThatResolvedConvertersResolvesItsOwn()
    {
        var first = new YamlSerializerOptions { Converters = [new TextConverter("first")] };
        var nested = new WritePathNested { Value = 5 };
        Assert.AreEqual("first\n", YamlSerializer.Serialize(nested, first));

        // The copy is made once the original resolved its converter, and changes what the converter is.
        var second = first with { Converters = [new TextConverter("second")] };
        var third = second with { IndentSize = 4 };

        Assert.AreEqual("second\n", YamlSerializer.Serialize(nested, second));
        Assert.AreEqual("second\n", YamlSerializer.Serialize(nested, third));
        Assert.AreEqual("first\n", YamlSerializer.Serialize(nested, first));
        Assert.AreEqual("Value: 5\n", YamlSerializer.Serialize(nested, first with { Converters = [] }));
    }

    private static bool IsAliveAfterCollections(WeakReference reference)
    {
        for (var i = 0; i < 10 && reference.IsAlive; i++)
        {
            GC.Collect();
            GC.WaitForPendingFinalizers();
        }

        return reference.IsAlive;
    }

    /// <summary>
    /// Serializes and deserializes an instance and a list of a type created in a collectible assembly, and returns
    /// the only reference left to that type.
    /// </summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static WeakReference UseCollectibleType(YamlSerializerOptions options)
    {
        var assembly = AssemblyBuilder.DefineDynamicAssembly(new AssemblyName("Collectible" + Guid.NewGuid().ToString("N")), AssemblyBuilderAccess.RunAndCollect);
        var type = assembly.DefineDynamicModule("Module").DefineType("Model", TypeAttributes.Public).CreateType()!;
        var instance = Activator.CreateInstance(type)!;
        var listType = typeof(List<>).MakeGenericType(type);
        var list = (IList)Activator.CreateInstance(listType)!;
        list.Add(instance);

        Assert.AreEqual("{}\n", YamlSerializer.Serialize(instance, type, options));
        Assert.AreEqual("- {}\n", YamlSerializer.Serialize(list, listType, options));
        Assert.IsInstanceOfType(YamlSerializer.Deserialize("{}", type, options), type);
        Assert.HasCount(1, (IList)YamlSerializer.Deserialize("- {}\n", listType, options)!);

        return new WeakReference(type);
    }

    private sealed class TextConverter : YamlConverter<WritePathNested>
    {
        private readonly string _text;

        public TextConverter(string text) => _text = text;

        public override WritePathNested Read(YamlReader reader) => throw new NotSupportedException();

        public override void Write(YamlWriter writer, WritePathNested value) => writer.WriteScalar(_text);
    }
}
