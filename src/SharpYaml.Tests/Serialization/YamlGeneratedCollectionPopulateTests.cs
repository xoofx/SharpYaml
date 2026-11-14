using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text.Json.Serialization;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SharpYaml.Serialization;

namespace SharpYaml.Tests.Serialization;

[TestClass]
public sealed class YamlGeneratedCollectionPopulateTests
{
    [TestMethod]
    public void Populate_ReadOnlySequences_AppendsGeneratedObjects()
    {
        var result = YamlSerializer.Deserialize("""
            Items: [{Name: first}, {Name: second}]
            Collection: [{Name: collection}]
            Concrete: [{Name: concrete}]
            Set: [{Name: set}]
            Mutable: [{Name: mutable}]
            """, CollectionPopulateContext.Default.CollectionPopulateRoot)!;

        CollectionAssert.AreEqual(new[] { "existing", "first", "second" }, result.Items.Select(x => x.Name).ToArray());
        Assert.AreEqual("collection", result.Collection.Single().Name);
        Assert.AreEqual("concrete", result.Concrete.Single().Name);
        Assert.AreEqual("set", result.Set.Single().Name);
        Assert.AreEqual("mutable", result.Mutable.Single().Name);
    }

    [TestMethod]
    public void Populate_NullWritableSequence_CreatesGeneratedObjects()
    {
        var result = YamlSerializer.Deserialize("Optional: [{Name: created}]", CollectionPopulateContext.Default.CollectionPopulateRoot)!;
        Assert.AreEqual("created", result.Optional!.Single().Name);
        result = YamlSerializer.Deserialize("Optional: null", CollectionPopulateContext.Default.CollectionPopulateRoot)!;
        Assert.IsNull(result.Optional);
        var exception = Assert.ThrowsExactly<YamlException>(() => YamlSerializer.Deserialize("Items: null", CollectionPopulateContext.Default.CollectionPopulateRoot));
        Assert.IsInstanceOfType<InvalidOperationException>(exception.InnerException);
    }

    [TestMethod]
    public void Populate_Anchors_RegisterExistingSequenceAndElements()
    {
        var context = new CollectionPopulateContext(new YamlSerializerOptions { ReferenceHandling = YamlReferenceHandling.Preserve });
        var result = YamlSerializer.Deserialize("""
            Items: &items [ &item {Name: shared}, *item ]
            Optional: *items
            """, context.CollectionPopulateRoot)!;

        Assert.AreSame(result.Items, result.Optional);
        Assert.AreSame(result.Items[1], result.Items[2]);
    }

    [TestMethod]
    public void Populate_UsesRuntimeElementConverter()
    {
        var context = new CollectionPopulateContext(new YamlSerializerOptions { Converters = [new PopulateItemConverter()] });
        var result = YamlSerializer.Deserialize("Items: [custom]", context.CollectionPopulateRoot)!;
        Assert.AreEqual("converted:custom", result.Items[1].Name);
    }

    [TestMethod]
    public void Populate_UsesRuntimeCollectionConverter()
    {
        var context = new CollectionPopulateContext(new YamlSerializerOptions { Converters = [new PopulateListConverter()] });
        var result = YamlSerializer.Deserialize("Items: ignored", context.CollectionPopulateRoot)!;
        Assert.AreEqual("populated", result.Items[1].Name);
    }

    [TestMethod]
    public void Populate_InvalidSequence_ThrowsYamlException()
    {
        Assert.Throws<YamlException>(() => YamlSerializer.Deserialize("Items: {}", CollectionPopulateContext.Default.CollectionPopulateRoot));
    }

    [TestMethod]
    public void Populate_Dictionaries_UsesGeneratedValuesAndPreservesInitialEntries()
    {
        var result = YamlSerializer.Deserialize("ByName: {added: {Name: generated}}\nByNumber: {1: {Name: typed}}\nOptionalMap: {new: {Name: optional}}", CollectionPopulateContext.Default.CollectionPopulateRoot)!;

        Assert.AreEqual("existing", result.ByName["keep"].Name);
        Assert.AreEqual("generated", result.ByName["added"].Name);
        Assert.AreEqual("typed", result.ByNumber[1].Name);
        Assert.AreEqual("optional", result.OptionalMap!["new"].Name);
        result = YamlSerializer.Deserialize("OptionalMap: null", CollectionPopulateContext.Default.CollectionPopulateRoot)!;
        Assert.IsNull(result.OptionalMap);
        Assert.ThrowsExactly<YamlException>(() => YamlSerializer.Deserialize("ByName: null", CollectionPopulateContext.Default.CollectionPopulateRoot));
        Assert.ThrowsExactly<YamlException>(() => YamlSerializer.Deserialize("ByName: []", CollectionPopulateContext.Default.CollectionPopulateRoot));
    }

    [TestMethod]
    public void Populate_DictionaryAnchors_RegisterTheExistingInstanceAndValues()
    {
        var context = new CollectionPopulateContext(new YamlSerializerOptions { ReferenceHandling = YamlReferenceHandling.Preserve });
        var result = YamlSerializer.Deserialize("ByName: &map {a: &value {Name: shared}, b: *value}\nOptionalMap: *map", context.CollectionPopulateRoot)!;

        Assert.AreSame(result.ByName, result.OptionalMap);
        Assert.AreSame(result.ByName["a"], result.ByName["b"]);
        Assert.AreEqual("existing", result.ByName["keep"].Name);
    }

    [TestMethod]
    public void Populate_Dictionary_UsesRuntimeValueAndCollectionConverters()
    {
        var context = new CollectionPopulateContext(new YamlSerializerOptions { Converters = [new PopulateItemConverter()] });
        var result = YamlSerializer.Deserialize("ByName: {new: custom}", context.CollectionPopulateRoot)!;
        Assert.AreEqual("converted:custom", result.ByName["new"].Name);

        context = new CollectionPopulateContext(new YamlSerializerOptions { Converters = [new PopulateDictionaryConverter()] });
        result = YamlSerializer.Deserialize("ByName: ignored", context.CollectionPopulateRoot)!;
        Assert.AreEqual("populated", result.ByName["custom"].Name);
    }

    [TestMethod]
    public void Populate_Dictionary_UsesRuntimeKeyConverters()
    {
        var context = new CollectionPopulateContext(new YamlSerializerOptions { Converters = [new PopulateKeyConverter()] });
        var result = YamlSerializer.Deserialize("ByNumber: {key: {Name: converted}}", context.CollectionPopulateRoot)!;
        Assert.AreEqual("converted", result.ByNumber[42].Name);
    }

    [TestMethod]
    public void Populate_Dictionary_UsesCompileTimeDuplicateHandling()
    {
        var context = new FirstWinsDictionaryPopulateContext(new YamlSerializerOptions { DuplicateKeyHandling = YamlDuplicateKeyHandling.Error });
        var result = YamlSerializer.Deserialize("ByNumber: {1: {Name: first}, 1: {Name: second}}", context.CollectionPopulateRoot)!;
        Assert.AreEqual("first", result.ByNumber[1].Name);
    }

    [TestMethod]
    public void Populate_Dictionary_UsesMemberAttributeConverter()
    {
        var result = YamlSerializer.Deserialize("AttributeMap: ignored", CollectionPopulateContext.Default.CollectionPopulateRoot)!;
        Assert.AreEqual("attribute", result.AttributeMap["custom"].Name);
    }

    private sealed class PopulateKeyConverter : YamlConverter<int>
    {
        public override int Read(YamlReader reader)
        {
            Assert.AreEqual("key", reader.GetScalarValue());
            reader.Read();
            return 42;
        }

        public override void Write(YamlWriter writer, int value) => throw new NotSupportedException();
    }

    private sealed class PopulateDictionaryConverter : YamlConverter<IDictionary<string, CollectionPopulateItem>>
    {
        public override bool CanPopulate(Type typeToConvert) => true;

        public override object Populate(YamlReader reader, Type typeToConvert, object existingValue)
        {
            ((IDictionary<string, CollectionPopulateItem>)existingValue)["custom"] = new() { Name = "populated" };
            reader.Skip();
            return existingValue;
        }

        public override IDictionary<string, CollectionPopulateItem> Read(YamlReader reader) => throw new NotSupportedException();
        public override void Write(YamlWriter writer, IDictionary<string, CollectionPopulateItem> value) => throw new NotSupportedException();
    }

    private sealed class PopulateItemConverter : YamlConverter<CollectionPopulateItem>
    {
        public override CollectionPopulateItem Read(YamlReader reader)
        {
            var result = new CollectionPopulateItem { Name = "converted:" + reader.ScalarValue };
            reader.Read();
            return result;
        }

        public override void Write(YamlWriter writer, CollectionPopulateItem value) => throw new NotSupportedException();
    }

    private sealed class PopulateListConverter : YamlConverter<IList<CollectionPopulateItem>>
    {
        public override bool CanPopulate(Type typeToConvert) => true;

        public override object Populate(YamlReader reader, Type typeToConvert, object existingValue)
        {
            ((IList<CollectionPopulateItem>)existingValue).Add(new CollectionPopulateItem { Name = "populated" });
            reader.Skip();
            return existingValue;
        }

        public override IList<CollectionPopulateItem> Read(YamlReader reader) => throw new NotSupportedException();
        public override void Write(YamlWriter writer, IList<CollectionPopulateItem> value) => throw new NotSupportedException();
    }
}

[JsonObjectCreationHandling(JsonObjectCreationHandling.Populate)]
internal sealed class CollectionPopulateRoot
{
    public IList<CollectionPopulateItem> Items { get; } = new List<CollectionPopulateItem> { new() { Name = "existing" } };
    public ICollection<CollectionPopulateItem> Collection { get; } = new List<CollectionPopulateItem>();
    public List<CollectionPopulateItem> Concrete { get; } = new();
    public ISet<CollectionPopulateItem> Set { get; } = new HashSet<CollectionPopulateItem>();
    public Collection<CollectionPopulateItem> Mutable { get; } = new();
    public IList<CollectionPopulateItem>? Optional { get; set; }
    public IDictionary<string, CollectionPopulateItem> ByName { get; } = new Dictionary<string, CollectionPopulateItem> { ["keep"] = new() { Name = "existing" } };
    public Dictionary<int, CollectionPopulateItem> ByNumber { get; } = new() { [1] = new() { Name = "existing" } };
    public IDictionary<string, CollectionPopulateItem>? OptionalMap { get; set; }

    [YamlConverter(typeof(AttributePopulateDictionaryConverter))]
    public IDictionary<string, CollectionPopulateItem> AttributeMap { get; } = new Dictionary<string, CollectionPopulateItem>();
}

internal sealed class CollectionPopulateItem
{
    public string Name { get; set; } = string.Empty;
}

internal sealed class AttributePopulateDictionaryConverter : YamlConverter<IDictionary<string, CollectionPopulateItem>>
{
    public override bool CanPopulate(Type typeToConvert) => true;

    public override object Populate(YamlReader reader, Type typeToConvert, object existingValue)
    {
        ((IDictionary<string, CollectionPopulateItem>)existingValue)["custom"] = new() { Name = "attribute" };
        reader.Skip();
        return existingValue;
    }

    public override IDictionary<string, CollectionPopulateItem> Read(YamlReader reader) => throw new NotSupportedException();
    public override void Write(YamlWriter writer, IDictionary<string, CollectionPopulateItem> value) => throw new NotSupportedException();
}

[YamlSerializable(typeof(CollectionPopulateRoot))]
internal partial class CollectionPopulateContext : YamlSerializerContext
{
    public CollectionPopulateContext()
    {
    }

    public CollectionPopulateContext(YamlSerializerOptions options) : base(options)
    {
    }
}

[YamlSourceGenerationOptions(DuplicateKeyHandling = YamlDuplicateKeyHandling.FirstWins)]
[YamlSerializable(typeof(CollectionPopulateRoot))]
internal partial class FirstWinsDictionaryPopulateContext : YamlSerializerContext
{
    public FirstWinsDictionaryPopulateContext()
    {
    }

    public FirstWinsDictionaryPopulateContext(YamlSerializerOptions options) : base(options)
    {
    }
}
