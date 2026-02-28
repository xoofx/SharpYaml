#nullable enable

using System;
using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace SharpYaml.Tests.Serialization;

[TestClass]
public class YamlReferenceHandlingTests
{
    private sealed class Node
    {
        public string Name { get; set; } = string.Empty;

        public Node? Next { get; set; }
    }

    private sealed class Container
    {
        public Node? A { get; set; }

        public Node? B { get; set; }
    }

    [TestMethod]
    public void SerializePreservesSelfReferenceForObjects()
    {
        var node = new Node { Name = "root" };
        node.Next = node;

        var yaml = SharpYaml.YamlSerializer.Serialize(
            node,
            new SharpYaml.YamlSerializerOptions { ReferenceHandling = SharpYaml.YamlReferenceHandling.Preserve });

        StringAssert.Contains(yaml, "&id001");
        StringAssert.Contains(yaml, "Next: *id001");
    }

    [TestMethod]
    public void DeserializePreservesSelfReferenceForObjects()
    {
        var yaml = "&id001\nName: root\nNext: *id001\n";

        var node = SharpYaml.YamlSerializer.Deserialize<Node>(
            yaml,
            new SharpYaml.YamlSerializerOptions { ReferenceHandling = SharpYaml.YamlReferenceHandling.Preserve });

        Assert.IsNotNull(node);
        Assert.IsTrue(ReferenceEquals(node, node.Next));
        Assert.AreEqual("root", node.Name);
    }

    [TestMethod]
    public void SerializePreservesSharedReferences()
    {
        var node = new Node { Name = "shared" };
        var container = new Container { A = node, B = node };

        var yaml = SharpYaml.YamlSerializer.Serialize(
            container,
            new SharpYaml.YamlSerializerOptions { ReferenceHandling = SharpYaml.YamlReferenceHandling.Preserve });

        var anchorStart = yaml.IndexOf("A: &", StringComparison.Ordinal);
        Assert.IsTrue(anchorStart >= 0, "Expected 'A' to be anchored.");
        anchorStart += "A: &".Length;

        var anchorEnd = yaml.IndexOf('\n', anchorStart);
        Assert.IsTrue(anchorEnd > anchorStart, "Expected an anchor name after 'A: &'.");

        var anchor = yaml.Substring(anchorStart, anchorEnd - anchorStart).Trim();
        Assert.IsTrue(anchor.Length > 0);

        StringAssert.Contains(yaml, $"B: *{anchor}");
    }

    [TestMethod]
    public void DeserializePreservesSharedReferences()
    {
        var yaml = "A: &id001\n  Name: shared\nB: *id001\n";

        var container = SharpYaml.YamlSerializer.Deserialize<Container>(
            yaml,
            new SharpYaml.YamlSerializerOptions { ReferenceHandling = SharpYaml.YamlReferenceHandling.Preserve });

        Assert.IsNotNull(container);
        Assert.IsNotNull(container.A);
        Assert.IsNotNull(container.B);
        Assert.IsTrue(ReferenceEquals(container.A, container.B));
        Assert.AreEqual("shared", container.A.Name);
    }

    [TestMethod]
    public void DeserializeAndSerializePreservesSelfReferenceForListsOfObject()
    {
        var yaml = "&id001\n- *id001\n";
        var options = new SharpYaml.YamlSerializerOptions { ReferenceHandling = SharpYaml.YamlReferenceHandling.Preserve };

        var list = SharpYaml.YamlSerializer.Deserialize<List<object?>>(yaml, options);

        Assert.IsNotNull(list);
        Assert.AreEqual(1, list.Count);
        Assert.IsTrue(ReferenceEquals(list, list[0]));

        var roundTrip = SharpYaml.YamlSerializer.Serialize(list, options);
        StringAssert.Contains(roundTrip, "&id001");
        StringAssert.Contains(roundTrip, "*id001");
    }

    [TestMethod]
    public void DeserializeAndSerializePreservesSelfReferenceForDictionariesOfObject()
    {
        var yaml = "&id001\nself: *id001\n";
        var options = new SharpYaml.YamlSerializerOptions { ReferenceHandling = SharpYaml.YamlReferenceHandling.Preserve };

        var dict = SharpYaml.YamlSerializer.Deserialize<Dictionary<string, object?>>(yaml, options);

        Assert.IsNotNull(dict);
        Assert.IsTrue(dict.TryGetValue("self", out var self));
        Assert.IsTrue(ReferenceEquals(dict, self));

        var roundTrip = SharpYaml.YamlSerializer.Serialize(dict, options);
        StringAssert.Contains(roundTrip, "&id001");
        StringAssert.Contains(roundTrip, "self: *id001");
    }
}
