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
        Assert.Throws<InvalidOperationException>(() => YamlSerializer.Deserialize("Items: null", CollectionPopulateContext.Default.CollectionPopulateRoot));
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
}

internal sealed class CollectionPopulateItem
{
    public string Name { get; set; } = string.Empty;
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
