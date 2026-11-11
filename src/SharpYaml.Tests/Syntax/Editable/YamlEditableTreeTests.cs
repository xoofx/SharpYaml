using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SharpYaml.Syntax.Editable;

namespace SharpYaml.Tests.Syntax.Editable;

[TestClass]
public class YamlEditableTreeTests
{
    public static IEnumerable<object[]> RoundTripCases()
    {
        yield return new object[]
        {
            "# comment\nroot:\n  key: value\n\nlist:\n  - a\n  - b\n",
        };
        yield return new object[]
        {
            "a: 1\r\nb:\r\n  - 2\r\n  - 3\r\n",
        };
        yield return new object[]
        {
            "server:\n  # inner comment\n  host: localhost\n  port: 5432\n",
        };
        yield return new object[]
        {
            "name: Ada\nServers:\n  - Name: prod\n    Port: 1521\n  - Name: dev\n    Port: 1522\n",
        };
        yield return new object[]
        {
            "server:\n  host: localhost\n  port: 5432\nclient:\n  timeout: 30\n",
        };
        yield return new object[]
        {
            "text: |\n  line 1\n  line 2\nnext: 1 # same line\n",
        };
        yield return new object[]
        {
            "key:\n  value # on the next line\nlast: 1",
        };
    }

    private const string ServersYaml =
        "# top\nServers:\n  # prod comment\n  - Name: prod # main\n    Port: 1521\n  # dev comment\n  - Name: dev\n    Port: 1522\nOther: x # other\n";

    private static YamlEditableMap RootOf(YamlEditableTree tree) => (YamlEditableMap)tree.Root;

    private static YamlEditableList ServersOf(YamlEditableTree tree) => (YamlEditableList)RootOf(tree).GetValue("Servers")!;

    private static YamlEditableMap ServerAt(YamlEditableTree tree, int index) => (YamlEditableMap)ServersOf(tree).Items[index].Value;

    private static YamlEditableListEntry NewServer(string name, string port)
    {
        var map = new YamlEditableMap();
        map.SetValue("Name", new YamlEditableScalar(name));
        map.SetValue("Port", new YamlEditableScalar(port), "    ");
        return new YamlEditableListEntry(map, "  ");
    }

    private static void AssertEdited(string expected, YamlEditableTree tree)
    {
        Assert.AreEqual(expected, tree.Serialize());
        Assert.AreEqual(expected, tree.ToSyntaxTree().ToFullString());
    }

    [TestMethod]
    public void AddEntry_ListOfMaps_StartsTheNewItemOnItsOwnLine()
    {
        var tree = YamlEditableTree.Parse(ServersYaml);

        ServersOf(tree).AddEntry(NewServer("test", "1523"));

        AssertEdited("# top\nServers:\n  # prod comment\n  - Name: prod # main\n    Port: 1521\n  # dev comment\n  - Name: dev\n    Port: 1522\n  - Name: test\n    Port: 1523\nOther: x # other\n", tree);
    }

    [TestMethod]
    public void RemoveEntry_LastKeyOfListItem_KeepsTheCommentOfThePreviousLine()
    {
        var tree = YamlEditableTree.Parse(ServersYaml);

        ServerAt(tree, 0).RemoveEntry("Port");

        AssertEdited("# top\nServers:\n  # prod comment\n  - Name: prod # main\n  # dev comment\n  - Name: dev\n    Port: 1522\nOther: x # other\n", tree);
    }

    [TestMethod]
    public void RemoveEntry_FirstKeyOfListItem_MovesTheNextKeyOntoTheDashLine()
    {
        var tree = YamlEditableTree.Parse(ServersYaml);

        ServerAt(tree, 0).RemoveEntry("Name");

        AssertEdited("# top\nServers:\n  # prod comment\n  - Port: 1521\n  # dev comment\n  - Name: dev\n    Port: 1522\nOther: x # other\n", tree);
    }

    [TestMethod]
    public void SetValue_NewKeyAtRoot_KeepsTheSameLineCommentOfTheLastKey()
    {
        var tree = YamlEditableTree.Parse(ServersYaml);

        RootOf(tree).SetValue("New", new YamlEditableScalar("y"));

        AssertEdited(ServersYaml + "New: y\n", tree);
    }

    [TestMethod]
    public void SetValue_ExistingKey_KeepsTheSameLineComment()
    {
        var tree = YamlEditableTree.Parse(ServersYaml);

        ServerAt(tree, 0).SetValue("Name", new YamlEditableScalar("stage"));

        AssertEdited(ServersYaml.Replace("Name: prod # main", "Name: stage # main"), tree);
    }

    [TestMethod]
    public void SetValue_NewKeyInSingleKeyListItem_AlignsUnderTheFirstKey()
    {
        var tree = YamlEditableTree.Parse("Items:\n  - Name: a\n");

        ((YamlEditableMap)((YamlEditableList)RootOf(tree).GetValue("Items")!).Items[0].Value).SetValue("Port", new YamlEditableScalar("1"));

        AssertEdited("Items:\n  - Name: a\n    Port: 1\n", tree);
    }

    [TestMethod]
    public void SetValue_NewKeyInDocumentWithoutFinalNewLine_KeepsNoFinalNewLine()
    {
        var tree = YamlEditableTree.Parse("a: 1");

        RootOf(tree).SetValue("b", new YamlEditableScalar("2"));

        AssertEdited("a: 1\nb: 2", tree);
    }

    [TestMethod]
    public void SetValue_ReplaceScalarWithMap_MovesTheMapToTheNextLine()
    {
        var tree = YamlEditableTree.Parse("a: 1 # keep\nb: 2\n");
        var map = new YamlEditableMap();
        map.SetValue("x", new YamlEditableScalar("1"), "  ");

        RootOf(tree).SetValue("a", map);

        AssertEdited("a: # keep\n  x: 1\nb: 2\n", tree);
    }

    [TestMethod]
    public void RemoveEntry_LastRootKey_RemovesItsComment()
    {
        var tree = YamlEditableTree.Parse(ServersYaml);

        RootOf(tree).RemoveEntry("Other");

        AssertEdited("# top\nServers:\n  # prod comment\n  - Name: prod # main\n    Port: 1521\n  # dev comment\n  - Name: dev\n    Port: 1522\n", tree);
    }

    [TestMethod]
    public void RemoveEntry_FirstRootKey_LeavesNoBlankLine()
    {
        var tree = YamlEditableTree.Parse(ServersYaml);

        RootOf(tree).RemoveEntry("Servers");

        AssertEdited("Other: x # other\n", tree);
    }

    [TestMethod]
    public void InsertEntry_ListItemAtStart_GoesBeforeTheCommentLinesOfTheFirstItem()
    {
        var tree = YamlEditableTree.Parse(ServersYaml);

        ServersOf(tree).InsertEntry(0, NewServer("test", "1523"));

        AssertEdited("# top\nServers:\n  - Name: test\n    Port: 1523\n  # prod comment\n  - Name: prod # main\n    Port: 1521\n  # dev comment\n  - Name: dev\n    Port: 1522\nOther: x # other\n", tree);
    }

    [TestMethod]
    public void InsertEntry_ListItemInTheMiddle_GoesBeforeTheCommentLinesOfTheNextItem()
    {
        var tree = YamlEditableTree.Parse(ServersYaml);

        ServersOf(tree).InsertEntry(1, NewServer("test", "1523"));

        AssertEdited("# top\nServers:\n  # prod comment\n  - Name: prod # main\n    Port: 1521\n  - Name: test\n    Port: 1523\n  # dev comment\n  - Name: dev\n    Port: 1522\nOther: x # other\n", tree);
    }

    [TestMethod]
    public void InsertEntry_ScalarListItem_KeepsTheOtherItems()
    {
        var tree = YamlEditableTree.Parse("items:\n  - a # first\n  - c\n");

        ((YamlEditableList)RootOf(tree).GetValue("items")!).InsertEntry(1, new YamlEditableListEntry(new YamlEditableScalar("b"), "  "));

        AssertEdited("items:\n  - a # first\n  - b\n  - c\n", tree);
    }

    [TestMethod]
    public void InsertEntry_KeyAtStartOfListItem_TakesTheDashLine()
    {
        var tree = YamlEditableTree.Parse(ServersYaml);

        ServerAt(tree, 0).InsertEntry(0, new YamlEditableMapEntry("Id", new YamlEditableScalar("1"), "    "));

        AssertEdited("# top\nServers:\n  # prod comment\n  - Id: 1\n    Name: prod # main\n    Port: 1521\n  # dev comment\n  - Name: dev\n    Port: 1522\nOther: x # other\n", tree);
    }

    [TestMethod]
    public void InsertEntry_KeyInTheMiddleOfRootMap_GoesBeforeTheNextKey()
    {
        var tree = YamlEditableTree.Parse("a: 1 # one\n# about c\nc: 3\n");

        RootOf(tree).InsertEntry(1, new YamlEditableMapEntry("b", new YamlEditableScalar("2"), string.Empty));

        AssertEdited("a: 1 # one\nb: 2\n# about c\nc: 3\n", tree);
    }

    [TestMethod]
    public void InsertEntry_IndexOutOfRange_ThrowsArgumentOutOfRangeException()
    {
        var tree = YamlEditableTree.Parse("a: 1\n");

        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() =>
            RootOf(tree).InsertEntry(2, new YamlEditableMapEntry("b", new YamlEditableScalar("2"), string.Empty)));
    }

    [TestMethod]
    public void RemoveAt_ListItems_RemovesTheirCommentLines()
    {
        var first = YamlEditableTree.Parse(ServersYaml);
        var last = YamlEditableTree.Parse(ServersYaml);

        ServersOf(first).RemoveAt(0);
        ServersOf(last).RemoveAt(1);

        AssertEdited("# top\nServers:\n  # dev comment\n  - Name: dev\n    Port: 1522\nOther: x # other\n", first);
        AssertEdited("# top\nServers:\n  # prod comment\n  - Name: prod # main\n    Port: 1521\nOther: x # other\n", last);
    }

    [TestMethod]
    [DynamicData(nameof(RoundTripCases))]
    public void ParseAndSerializeWithoutEditingPreservesOriginalText(string yaml)
    {
        var tree = YamlEditableTree.Parse(yaml);

        Assert.AreEqual(yaml, tree.Serialize());

        using var writer = new StringWriter();
        tree.WriteTo(writer);
        Assert.AreEqual(yaml, writer.ToString());
    }

    [TestMethod]
    public void SetValue_OnExistingScalarKey_PreservesSurroundingComments()
    {
        const string yaml = "# top comment\nroot:\n  key: value\n  other: 1\n";
        var tree = YamlEditableTree.Parse(yaml);
        var root = (YamlEditableMap)tree.Root;
        var inner = (YamlEditableMap)root.GetValue("root")!;

        inner.SetValue("key", new YamlEditableScalar("changed"));

        var result = tree.Serialize();
        Assert.Contains("# top comment", result);
        Assert.Contains("key: changed", result);
        Assert.Contains("other: 1", result);
        Assert.DoesNotContain("key: value", result);
    }

    [TestMethod]
    public void SetValue_AddsNewKey_AtRootMap()
    {
        const string yaml = "a: 1\nb: 2\n";
        var tree = YamlEditableTree.Parse(yaml);
        var root = (YamlEditableMap)tree.Root;

        root.SetValue("c", new YamlEditableScalar("3"));

        var result = tree.Serialize();
        Assert.Contains("a: 1\n", result);
        Assert.Contains("b: 2\n", result);
        Assert.Contains("c: 3", result);
    }

    [TestMethod]
    public void RemoveEntry_ExistingKey_DropsItFromSerializedOutput()
    {
        const string yaml = "a: 1\nb: 2\nc: 3\n";
        var tree = YamlEditableTree.Parse(yaml);
        var root = (YamlEditableMap)tree.Root;

        var removed = root.RemoveEntry("b");

        Assert.IsTrue(removed);
        var result = tree.Serialize();
        Assert.Contains("a: 1", result);
        Assert.Contains("c: 3", result);
        Assert.DoesNotContain("b: 2", result);
    }

    [TestMethod]
    public void Parse_ListOfScalars_ExposesItemsInOrder()
    {
        const string yaml = "items:\n  - a\n  - b\n  - c\n";
        var tree = YamlEditableTree.Parse(yaml);
        var root = (YamlEditableMap)tree.Root;
        var list = (YamlEditableList)root.GetValue("items")!;

        Assert.HasCount(3, list.Items);
        Assert.AreEqual("a", ((YamlEditableScalar)list.Items[0].Value).Text);
        Assert.AreEqual("b", ((YamlEditableScalar)list.Items[1].Value).Text);
        Assert.AreEqual("c", ((YamlEditableScalar)list.Items[2].Value).Text);
    }

    [TestMethod]
    public void Parse_ListOfMultiKeyMaps_ExposesEveryKeyOfEachItem()
    {
        // The first key of each item shares its line with "-", so it has no indentation of its
        // own; the next key ("Port") is on its own line. Both must stay in the same map.
        const string yaml = "Servers:\n  - Name: prod\n    Port: 1521\n  - Name: dev\n    Port: 1522\n";
        var tree = YamlEditableTree.Parse(yaml);
        var root = (YamlEditableMap)tree.Root;
        var servers = (YamlEditableList)root.GetValue("Servers")!;

        Assert.HasCount(2, servers.Items);
        var first = (YamlEditableMap)servers.Items[0].Value;
        Assert.AreEqual("prod", ((YamlEditableScalar)first.GetValue("Name")!).Text);
        Assert.AreEqual("1521", ((YamlEditableScalar)first.GetValue("Port")!).Text);
        var second = (YamlEditableMap)servers.Items[1].Value;
        Assert.AreEqual("dev", ((YamlEditableScalar)second.GetValue("Name")!).Text);
        Assert.AreEqual("1522", ((YamlEditableScalar)second.GetValue("Port")!).Text);
    }

    [TestMethod]
    public void Parse_DedentAfterNestedMap_EndsNestedMapAtParentLevel()
    {
        const string yaml = "server:\n  host: localhost\n  port: 5432\nclient:\n  timeout: 30\n";
        var tree = YamlEditableTree.Parse(yaml);
        var root = (YamlEditableMap)tree.Root;

        var server = (YamlEditableMap)root.GetValue("server")!;
        var client = (YamlEditableMap)root.GetValue("client")!;
        Assert.AreEqual("5432", ((YamlEditableScalar)server.GetValue("port")!).Text);
        Assert.IsNull(server.GetValue("client"));
        Assert.AreEqual("30", ((YamlEditableScalar)client.GetValue("timeout")!).Text);
    }

    private static string TextOf(YamlEditableNode? node) => ((YamlEditableScalar)node!).Text;

    [TestMethod]
    [DataRow("A:\nB: 1\n")]
    [DataRow("A:\n")]
    [DataRow("A:")]
    [DataRow("A: 1\nB:\n")]
    [DataRow("A: # no value\nB: 1\n")]
    public void Parse_KeyWithoutValue_IsAnEmptyScalarAndRoundTrips(string yaml)
    {
        var tree = YamlEditableTree.Parse(yaml);
        var root = RootOf(tree);

        var key = yaml.StartsWith("A: 1", StringComparison.Ordinal) ? "B" : "A";
        Assert.AreEqual(string.Empty, TextOf(root.GetValue(key)));
        Assert.AreEqual(yaml, tree.Serialize());
    }

    [TestMethod]
    public void Parse_KeyWithoutValueBeforeSiblingKey_DoesNotNestTheSibling()
    {
        var root = RootOf(YamlEditableTree.Parse("L:\n  - 1\nA:\nB: 2\n"));

        Assert.AreEqual(string.Empty, TextOf(root.GetValue("A")));
        Assert.AreEqual("2", TextOf(root.GetValue("B")));
    }

    [TestMethod]
    public void Parse_NestedKeyWithoutValue_DoesNotNestTheNextKey()
    {
        var root = RootOf(YamlEditableTree.Parse("M:\n  X:\n  Y: 1\nB: 2\n"));
        var m = (YamlEditableMap)root.GetValue("M")!;

        Assert.AreEqual(string.Empty, TextOf(m.GetValue("X")));
        Assert.AreEqual("1", TextOf(m.GetValue("Y")));
        Assert.AreEqual("2", TextOf(root.GetValue("B")));
    }

    [TestMethod]
    public void Parse_FirstKeyOfListItemWithoutValue_KeepsTheNextKeyInTheSameItem()
    {
        const string yaml = "S:\n  - Name:\n    Port: 1\n";
        var tree = YamlEditableTree.Parse(yaml);
        var item = (YamlEditableMap)((YamlEditableList)RootOf(tree).GetValue("S")!).Items[0].Value;

        Assert.AreEqual(string.Empty, TextOf(item.GetValue("Name")));
        Assert.AreEqual("1", TextOf(item.GetValue("Port")));
        Assert.AreEqual(yaml, tree.Serialize());
    }

    [TestMethod]
    public void Parse_ListItemWithoutValue_IsAnEmptyScalarAndRoundTrips()
    {
        const string yaml = "L:\n  -\n  - 1\n";
        var tree = YamlEditableTree.Parse(yaml);
        var list = (YamlEditableList)RootOf(tree).GetValue("L")!;

        Assert.HasCount(2, list.Items);
        Assert.AreEqual(string.Empty, TextOf(list.Items[0].Value));
        Assert.AreEqual(yaml, tree.Serialize());
    }

    [TestMethod]
    [DataRow("A:\n- 1\n- 2\nB: 3\n")]
    [DataRow("A:\n- x: 1\n  y: 2\n- x: 3\nB: 3\n")]
    [DataRow("M:\n  A:\n  - 1\n  C: 2\nB: 3\n")]
    [DataRow("S:\n  - x: 1\nB: 3\n")]
    [DataRow("S:\n  - x: 1\n    y: 2\n  - x: 3\nB: 3\n")]
    public void Parse_SequenceAtTheKeyColumn_IsTheValueOfTheKeyAndRoundTrips(string yaml)
    {
        var tree = YamlEditableTree.Parse(yaml);
        var root = RootOf(tree);

        Assert.AreEqual("3", TextOf(root.GetValue("B")));
        Assert.AreEqual(yaml, tree.Serialize());
    }

    [TestMethod]
    public void SetValue_OnKeyWithoutValue_WritesASpaceAfterTheColon()
    {
        var tree = YamlEditableTree.Parse("A:\nB: 1\n");

        RootOf(tree).SetValue("A", new YamlEditableScalar("x"));

        AssertEdited("A: x\nB: 1\n", tree);
    }

    [TestMethod]
    public void Parse_EmptyFlowCollections_AreEmptyNodesAndRoundTrip()
    {
        const string yaml = "A: []\nB: {} # none\nL:\n  - []\n";
        var tree = YamlEditableTree.Parse(yaml);
        var root = RootOf(tree);

        Assert.IsEmpty(((YamlEditableList)root.GetValue("A")!).Items);
        Assert.IsEmpty(((YamlEditableMap)root.GetValue("B")!).Entries);
        Assert.IsEmpty(((YamlEditableList)((YamlEditableList)root.GetValue("L")!).Items[0].Value).Items);
        Assert.AreEqual(yaml, tree.Serialize());
    }

    [TestMethod]
    [DataRow("A: [1]\n")]
    [DataRow("A: {x: 1}\n")]
    [DataRow("A: [ ]\n")]
    public void Parse_NonEmptyFlowCollection_ThrowsNotSupportedException(string yaml)
    {
        Assert.ThrowsExactly<NotSupportedException>(() => YamlEditableTree.Parse(yaml));
    }

    [TestMethod]
    public void SetValue_NewEmptyCollections_AreWrittenInFlowStyle()
    {
        var tree = YamlEditableTree.Parse("x: 1\n");
        var root = RootOf(tree);

        root.SetValue("A", new YamlEditableList());
        root.SetValue("B", new YamlEditableMap());

        AssertEdited("x: 1\nA: []\nB: {}\n", tree);
    }

    [TestMethod]
    public void Parse_EmptyDocument_StillSerializesToNothing()
    {
        Assert.AreEqual(string.Empty, YamlEditableTree.Parse(string.Empty).Serialize());
    }

    [TestMethod]
    public void ToSyntaxTree_WithoutEdits_ReturnsSourceSnapshot()
    {
        var tree = YamlEditableTree.Parse("a: 1 # keep\n");

        Assert.AreSame(tree.SyntaxTree, tree.ToSyntaxTree());
    }

    [TestMethod]
    public void ToSyntaxTree_AfterEdit_ReturnsValidatedTreeAndKeepsSourceSnapshot()
    {
        const string yaml = "# top\nServers:\n  - Name: prod # main\n    Port: 1521\n";
        var tree = YamlEditableTree.Parse(yaml);
        var servers = (YamlEditableList)((YamlEditableMap)tree.Root).GetValue("Servers")!;
        ((YamlEditableMap)servers.Items[0].Value).SetValue("Port", new YamlEditableScalar("1522"));

        var edited = tree.ToSyntaxTree();

        Assert.AreEqual("# top\nServers:\n  - Name: prod # main\n    Port: 1522\n", edited.ToFullString());
        Assert.AreEqual(tree.Serialize(), edited.ToFullString());
        Assert.AreEqual(yaml, tree.SyntaxTree.ToFullString());
    }

    [TestMethod]
    public void ToSyntaxTree_DocumentationExample_ProducesDocumentedOutput()
    {
        // Same code as site/docs/low-level/syntax-tree.md, "Edit by key with the editable tree".
        var tree = YamlEditableTree.Parse("# servers\nServers:\n  - Name: prod # main\n    Port: 1521\n");
        var servers = (YamlEditableList)((YamlEditableMap)tree.Root).GetValue("Servers")!;
        var prod = (YamlEditableMap)servers.Items[0].Value;
        prod.SetValue("Port", new YamlEditableScalar("1522"));
        prod.SetValue("Host", new YamlEditableScalar("db1"));

        var edited = tree.ToSyntaxTree();

        Assert.AreEqual("# servers\nServers:\n  - Name: prod # main\n    Port: 1522\n    Host: db1\n", edited.ToFullString());
    }

    [TestMethod]
    public void ToSyntaxTree_EditProducesInvalidYaml_ThrowsYamlException()
    {
        var tree = YamlEditableTree.Parse("a: 1\n");
        ((YamlEditableMap)tree.Root).SetValue("a", new YamlEditableScalar("[unclosed"));

        Assert.Throws<YamlException>(() => tree.ToSyntaxTree());
    }

    [TestMethod]
    public void Parse_FlowStyleMapping_ThrowsNotSupportedException()
    {
        const string yaml = "flow: { a: 1, b: 2 }\n";
        Assert.ThrowsExactly<NotSupportedException>(() => YamlEditableTree.Parse(yaml));
    }

    [TestMethod]
    public void Parse_Anchor_ThrowsNotSupportedException()
    {
        const string yaml = "node: &n1 value\nalias: *n1\n";
        Assert.ThrowsExactly<NotSupportedException>(() => YamlEditableTree.Parse(yaml));
    }
}
