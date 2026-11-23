using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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
        yield return new object[] { "{}\n" };
        yield return new object[] { "[]" };
        yield return new object[] { "[ ]\n" };
        yield return new object[] { "# one\n{ }\n" };
        yield return new object[] { "a: [ ]\nb: {  } # two\n" };
        yield return new object[] { "# one\nvalue\n" };
        yield return new object[] { "\n# one\n  value # two" };
        yield return new object[] { "k:\n  - - a\n    - b\n" };
        yield return new object[] { "-\n a: 1\n b: 2\n" };
    }

    private const string ServersYaml =
        "# top\nServers:\n  # prod comment\n  - Name: prod # main\n    Port: 1521\n  # dev comment\n  - Name: dev\n    Port: 1522\nOther: x # other\n";

    private static YamlEditableMapping RootOf(YamlEditableTree tree) => (YamlEditableMapping)tree.Root;

    private static List<YamlEditableScalar> Scalars(YamlEditableNode node) => node switch
    {
        YamlEditableScalar scalar => [scalar],
        YamlEditableMapping map => map.Entries.SelectMany(entry => Scalars(entry.Value)).ToList(),
        YamlEditableSequence list => list.Items.SelectMany(item => Scalars(item.Value)).ToList(),
        _ => [],
    };

    private static YamlEditableSequence ServersOf(YamlEditableTree tree) => (YamlEditableSequence)RootOf(tree).GetValue("Servers")!;

    private static YamlEditableMapping ServerAt(YamlEditableTree tree, int index) => (YamlEditableMapping)ServersOf(tree).Items[index].Value;

    private static YamlEditableSequenceEntry NewServer(string name, string port)
    {
        var map = new YamlEditableMapping();
        map.SetValue("Name", new YamlEditableScalar(name));
        map.SetValue("Port", new YamlEditableScalar(port), "    ");
        return new YamlEditableSequenceEntry(map, "  ");
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

        ((YamlEditableMapping)((YamlEditableSequence)RootOf(tree).GetValue("Items")!).Items[0].Value).SetValue("Port", new YamlEditableScalar("1"));

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
        var map = new YamlEditableMapping();
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

        ((YamlEditableSequence)RootOf(tree).GetValue("items")!).InsertEntry(1, new YamlEditableSequenceEntry(new YamlEditableScalar("b"), "  "));

        AssertEdited("items:\n  - a # first\n  - b\n  - c\n", tree);
    }

    [TestMethod]
    public void InsertEntry_KeyAtStartOfListItem_TakesTheDashLine()
    {
        var tree = YamlEditableTree.Parse(ServersYaml);

        ServerAt(tree, 0).InsertEntry(0, new YamlEditableMappingEntry("Id", new YamlEditableScalar("1"), "    "));

        AssertEdited("# top\nServers:\n  # prod comment\n  - Id: 1\n    Name: prod # main\n    Port: 1521\n  # dev comment\n  - Name: dev\n    Port: 1522\nOther: x # other\n", tree);
    }

    [TestMethod]
    public void InsertEntry_KeyInTheMiddleOfRootMap_GoesBeforeTheNextKey()
    {
        var tree = YamlEditableTree.Parse("a: 1 # one\n# about c\nc: 3\n");

        RootOf(tree).InsertEntry(1, new YamlEditableMappingEntry("b", new YamlEditableScalar("2"), string.Empty));

        AssertEdited("a: 1 # one\nb: 2\n# about c\nc: 3\n", tree);
    }

    [TestMethod]
    public void InsertEntry_IndexOutOfRange_ThrowsArgumentOutOfRangeException()
    {
        var tree = YamlEditableTree.Parse("a: 1\n");

        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() =>
            RootOf(tree).InsertEntry(2, new YamlEditableMappingEntry("b", new YamlEditableScalar("2"), string.Empty)));
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
        var root = (YamlEditableMapping)tree.Root;
        var inner = (YamlEditableMapping)root.GetValue("root")!;

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
        var root = (YamlEditableMapping)tree.Root;

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
        var root = (YamlEditableMapping)tree.Root;

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
        var root = (YamlEditableMapping)tree.Root;
        var list = (YamlEditableSequence)root.GetValue("items")!;

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
        var root = (YamlEditableMapping)tree.Root;
        var servers = (YamlEditableSequence)root.GetValue("Servers")!;

        Assert.HasCount(2, servers.Items);
        var first = (YamlEditableMapping)servers.Items[0].Value;
        Assert.AreEqual("prod", ((YamlEditableScalar)first.GetValue("Name")!).Text);
        Assert.AreEqual("1521", ((YamlEditableScalar)first.GetValue("Port")!).Text);
        var second = (YamlEditableMapping)servers.Items[1].Value;
        Assert.AreEqual("dev", ((YamlEditableScalar)second.GetValue("Name")!).Text);
        Assert.AreEqual("1522", ((YamlEditableScalar)second.GetValue("Port")!).Text);
    }

    [TestMethod]
    public void Parse_DedentAfterNestedMap_EndsNestedMapAtParentLevel()
    {
        const string yaml = "server:\n  host: localhost\n  port: 5432\nclient:\n  timeout: 30\n";
        var tree = YamlEditableTree.Parse(yaml);
        var root = (YamlEditableMapping)tree.Root;

        var server = (YamlEditableMapping)root.GetValue("server")!;
        var client = (YamlEditableMapping)root.GetValue("client")!;
        Assert.AreEqual("5432", ((YamlEditableScalar)server.GetValue("port")!).Text);
        Assert.IsNull(server.GetValue("client"));
        Assert.AreEqual("30", ((YamlEditableScalar)client.GetValue("timeout")!).Text);
    }

    [TestMethod]
    [DataRow("Items:\n  - Id: b\n    Tags:\n      - x\n  - Id: c\n")]
    [DataRow("Items:\n  - Id: b\n    Tags:\n    - x\n  - Id: c\n")]
    [DataRow("- Tags:\n    - x\n- c\n")]
    public void Parse_NestedSequenceInListItem_EndsBeforeTheNextOuterItem(string yaml)
    {
        // The "-" of the next outer item is at another column than the "-" of the nested sequence:
        // it belongs to the outer sequence.
        var tree = YamlEditableTree.Parse(yaml);
        var outer = tree.Root is YamlEditableMapping map ? (YamlEditableSequence)map.GetValue("Items")! : (YamlEditableSequence)tree.Root;

        Assert.HasCount(2, outer.Items);
        var tags = (YamlEditableSequence)((YamlEditableMapping)outer.Items[0].Value).GetValue("Tags")!;
        Assert.HasCount(1, tags.Items);
        Assert.AreEqual("x", TextOf(tags.Items[0].Value));
        Assert.AreEqual(yaml, tree.Serialize());
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
        var m = (YamlEditableMapping)root.GetValue("M")!;

        Assert.AreEqual(string.Empty, TextOf(m.GetValue("X")));
        Assert.AreEqual("1", TextOf(m.GetValue("Y")));
        Assert.AreEqual("2", TextOf(root.GetValue("B")));
    }

    [TestMethod]
    public void Parse_FirstKeyOfListItemWithoutValue_KeepsTheNextKeyInTheSameItem()
    {
        const string yaml = "S:\n  - Name:\n    Port: 1\n";
        var tree = YamlEditableTree.Parse(yaml);
        var item = (YamlEditableMapping)((YamlEditableSequence)RootOf(tree).GetValue("S")!).Items[0].Value;

        Assert.AreEqual(string.Empty, TextOf(item.GetValue("Name")));
        Assert.AreEqual("1", TextOf(item.GetValue("Port")));
        Assert.AreEqual(yaml, tree.Serialize());
    }

    [TestMethod]
    public void Parse_ListItemWithoutValue_IsAnEmptyScalarAndRoundTrips()
    {
        const string yaml = "L:\n  -\n  - 1\n";
        var tree = YamlEditableTree.Parse(yaml);
        var list = (YamlEditableSequence)RootOf(tree).GetValue("L")!;

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

        Assert.IsEmpty(((YamlEditableSequence)root.GetValue("A")!).Items);
        Assert.IsEmpty(((YamlEditableMapping)root.GetValue("B")!).Entries);
        Assert.IsEmpty(((YamlEditableSequence)((YamlEditableSequence)root.GetValue("L")!).Items[0].Value).Items);
        Assert.AreEqual(yaml, tree.Serialize());
    }

    [TestMethod]
    [DataRow("A: [1]\n")]
    [DataRow("A: {x: 1}\n")]
    [DataRow("A: [\n  ]\n")]
    [DataRow("A: [ # one\n  ]\n")]
    public void Parse_FlowCollectionWithContentOrOnSeveralLines_ThrowsNotSupportedException(string yaml)
    {
        Assert.ThrowsExactly<NotSupportedException>(() => YamlEditableTree.Parse(yaml));
    }

    [TestMethod]
    public void SetValue_NewEmptyCollections_AreWrittenInFlowStyle()
    {
        var tree = YamlEditableTree.Parse("x: 1\n");
        var root = RootOf(tree);

        root.SetValue("A", new YamlEditableSequence());
        root.SetValue("B", new YamlEditableMapping());

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
        var servers = (YamlEditableSequence)((YamlEditableMapping)tree.Root).GetValue("Servers")!;
        ((YamlEditableMapping)servers.Items[0].Value).SetValue("Port", new YamlEditableScalar("1522"));

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
        var servers = (YamlEditableSequence)((YamlEditableMapping)tree.Root).GetValue("Servers")!;
        var prod = (YamlEditableMapping)servers.Items[0].Value;
        var name = ((YamlEditableScalar)prod.GetValue("Name")!).Value;
        prod.SetValue("Port", new YamlEditableScalar("1522"));
        prod.SetValue("Host", new YamlEditableScalar("db1"));

        var edited = tree.ToSyntaxTree();

        Assert.AreEqual("prod", name);
        Assert.AreEqual("# servers\nServers:\n  - Name: prod # main\n    Port: 1522\n    Host: db1\n", edited.ToFullString());
    }

    [TestMethod]
    public void ToSyntaxTree_EditProducesInvalidYaml_ThrowsYamlException()
    {
        var tree = YamlEditableTree.Parse("a: 1\n");
        ((YamlEditableMapping)tree.Root).SetValue("a", new YamlEditableScalar("[unclosed"));

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

    [TestMethod]
    [DataRow("---\na: 1\n")]
    [DataRow("a: 1\n...\n")]
    [DataRow("a: 1\n---\nb: 2\n")]
    public void Parse_DocumentMarker_ThrowsNotSupportedException(string yaml)
    {
        Assert.ThrowsExactly<NotSupportedException>(() => YamlEditableTree.Parse(yaml));
    }

    [TestMethod]
    public void GetValue_QuotedKeys_FindsThemByTheirValue()
    {
        var root = RootOf(YamlEditableTree.Parse("\"name\": x\n'other': y\n\"a\\tb\": z\n"));

        Assert.AreEqual("x", ((YamlEditableScalar)root.GetValue("name")!).Text);
        Assert.AreEqual("y", ((YamlEditableScalar)root.GetValue("other")!).Text);
        Assert.AreEqual("z", ((YamlEditableScalar)root.GetValue("a\tb")!).Text);
    }

    [TestMethod]
    public void SetValue_QuotedExistingKey_ReplacesItsValue()
    {
        var tree = YamlEditableTree.Parse("\"name\": x\n");

        RootOf(tree).SetValue("name", new YamlEditableScalar("z"));

        AssertEdited("\"name\": z\n", tree);
    }

    [TestMethod]
    public void RemoveEntry_QuotedKey_RemovesTheEntry()
    {
        var tree = YamlEditableTree.Parse("'a': 1\nb: 2\n");

        Assert.IsTrue(RootOf(tree).RemoveEntry("a"));

        AssertEdited("b: 2\n", tree);
    }

    [TestMethod]
    public void SetValue_ReplaceLiteralScalar_KeepsTheLineBreak()
    {
        var tree = YamlEditableTree.Parse("a: |\n  line1\nb: 2\n");

        RootOf(tree).SetValue("a", new YamlEditableScalar("x"));

        AssertEdited("a: x\nb: 2\n", tree);
    }

    [TestMethod]
    public void SetValue_ReplaceLiteralScalarBeforeCommentLine_KeepsTheCommentLine()
    {
        var tree = YamlEditableTree.Parse("a: |\n  x\n# c\nb: 1\n");

        RootOf(tree).SetValue("a", new YamlEditableScalar("y"));

        AssertEdited("a: y\n# c\nb: 1\n", tree);
    }

    [TestMethod]
    public void SetValue_NewKeyAfterFoldedScalarAtTheEnd_AddsNoBlankLine()
    {
        var tree = YamlEditableTree.Parse("b: 2\na: >\n  folded\n");

        RootOf(tree).SetValue("c", new YamlEditableScalar("4"));

        AssertEdited("b: 2\na: >\n  folded\nc: 4\n", tree);
    }

    [TestMethod]
    [DataRow("a: |\n  line1\n\nb: 2\n")]
    [DataRow("a: |- # c\n  x\nb: 1\n")]
    [DataRow("a: |\n  x\n# c\nb: 1\n")]
    [DataRow("m:\n  a: |\n    x\n  b: 1\n")]
    [DataRow("- |\n  x\n- y\n")]
    [DataRow("a: |\n  x")]
    [DataRow("a: |\r\n  x\r\nb: 1\r\n")]
    public void Parse_BlockScalar_RoundTrips(string yaml)
    {
        Assert.AreEqual(yaml, YamlEditableTree.Parse(yaml).Serialize());
    }

    [TestMethod]
    public void SetValue_NewKeyWithMapWithoutIndent_IndentsTheMapUnderTheKey()
    {
        var tree = YamlEditableTree.Parse("a: 1\n");
        var map = new YamlEditableMapping();
        map.SetValue("x", new YamlEditableScalar("1"));
        map.SetValue("y", new YamlEditableScalar("2"));

        RootOf(tree).SetValue("c", map);

        AssertEdited("a: 1\nc:\n  x: 1\n  y: 2\n", tree);
    }

    [TestMethod]
    public void SetValue_NewKeyWithNestedMapsWithoutIndent_IndentsEachLevel()
    {
        var tree = YamlEditableTree.Parse("a: 1\n");
        var inner = new YamlEditableMapping();
        inner.SetValue("y", new YamlEditableScalar("1"));
        var outer = new YamlEditableMapping();
        outer.SetValue("x", inner);

        RootOf(tree).SetValue("c", outer);

        AssertEdited("a: 1\nc:\n  x:\n    y: 1\n", tree);
    }

    [TestMethod]
    public void SetValue_ReplaceNestedScalarWithMapWithoutIndent_IndentsTheMapUnderTheKey()
    {
        var tree = YamlEditableTree.Parse("m:\n  a: 1\n");
        var map = new YamlEditableMapping();
        map.SetValue("x", new YamlEditableScalar("1"));

        ((YamlEditableMapping)RootOf(tree).GetValue("m")!).SetValue("a", map);

        AssertEdited("m:\n  a:\n    x: 1\n", tree);
    }

    [TestMethod]
    public void AddEntry_ListItemMapWithoutIndent_AlignsTheKeysUnderTheFirstKey()
    {
        var tree = YamlEditableTree.Parse("Servers:\n  - Name: prod\n");
        var map = new YamlEditableMapping();
        map.SetValue("Name", new YamlEditableScalar("test"));
        map.SetValue("Port", new YamlEditableScalar("1523"));

        ServersOf(tree).AddEntry(new YamlEditableSequenceEntry(map, "  "));

        AssertEdited("Servers:\n  - Name: prod\n  - Name: test\n    Port: 1523\n", tree);
    }

    [TestMethod]
    public void SetValue_NewKeyWithMapInCrLfDocument_UsesCrLf()
    {
        var tree = YamlEditableTree.Parse("a: 1\r\nb: 2\r\n");
        var map = new YamlEditableMapping();
        map.SetValue("x", new YamlEditableScalar("1"), "  ");
        map.SetValue("y", new YamlEditableScalar("2"));

        RootOf(tree).SetValue("c", map);

        AssertEdited("a: 1\r\nb: 2\r\nc:\r\n  x: 1\r\n  y: 2\r\n", tree);
    }

    [TestMethod]
    public void SetValue_ReplaceScalarWithMapInCrLfDocument_UsesCrLf()
    {
        var tree = YamlEditableTree.Parse("a: 1\r\nb: 2\r\n");
        var map = new YamlEditableMapping();
        map.SetValue("x", new YamlEditableScalar("1"), "  ");
        map.SetValue("y", new YamlEditableScalar("2"));

        RootOf(tree).SetValue("a", map);

        AssertEdited("a:\r\n  x: 1\r\n  y: 2\r\nb: 2\r\n", tree);
    }

    [TestMethod]
    public void Parse_ByteOrderMark_IsKeptWithAndWithoutEdits()
    {
        var tree = YamlEditableTree.Parse("\uFEFFa: 1\n");
        Assert.AreEqual("\uFEFFa: 1\n", tree.Serialize());
        Assert.AreSame(tree.SyntaxTree, tree.ToSyntaxTree());

        RootOf(tree).SetValue("a", new YamlEditableScalar("2"));

        AssertEdited("\uFEFFa: 2\n", tree);
    }

    [TestMethod]
    // The comment lines of the document stay its own: a blank line separates them from the first entry.
    [DataRow("# only\n", "# only\n\na: 1\n")]
    [DataRow("# only", "# only\n\na: 1")]
    [DataRow("# one\r\n\r\n# two\r\n", "# one\r\n\r\n# two\r\n\r\na: 1\r\n")]
    [DataRow("# one\n\n", "# one\n\na: 1\n")]
    public void SetValue_DocumentWithOnlyComments_AddsTheKeyAfterABlankLine(string yaml, string expected)
    {
        var tree = YamlEditableTree.Parse(yaml);

        RootOf(tree).SetValue("a", new YamlEditableScalar("1"));

        AssertEdited(expected, tree);
    }

    [TestMethod]
    public void SetValue_NewKeyAfterCommentWithoutFinalNewLine_KeepsTheCommentOnItsLine()
    {
        var tree = YamlEditableTree.Parse("a: 1 # one");

        RootOf(tree).SetValue("b", new YamlEditableScalar("2"));

        AssertEdited("a: 1 # one\nb: 2", tree);
    }

    [TestMethod]
    public void SetValue_NewKeyInCrLfDocumentWithoutFinalNewLine_UsesCrLf()
    {
        var tree = YamlEditableTree.Parse("a: 1\r\nb: 2");

        RootOf(tree).SetValue("c", new YamlEditableScalar("3"));

        AssertEdited("a: 1\r\nb: 2\r\nc: 3", tree);
    }

    [TestMethod]
    [DataRow("a:\n  b: 1\nc: 2\n", "a: {}\nc: 2\n")]
    [DataRow("a: # one\n  b: 1\nc: 2\n", "a: {} # one\nc: 2\n")]
    [DataRow("a:\r\n  b: 1\r\n", "a: {}\r\n")]
    public void RemoveEntry_OnlyKeyOfNestedMap_WritesAnEmptyMapOnTheKeyLine(string yaml, string expected)
    {
        var tree = YamlEditableTree.Parse(yaml);

        ((YamlEditableMapping)RootOf(tree).GetValue("a")!).RemoveEntry("b");

        AssertEdited(expected, tree);
    }

    [TestMethod]
    public void RemoveEntry_OnlyKeyOfListItem_WritesAnEmptyMapAfterTheDash()
    {
        var tree = YamlEditableTree.Parse("- x: 1\n- b\n");

        ((YamlEditableMapping)((YamlEditableSequence)tree.Root).Items[0].Value).RemoveEntry("x");

        AssertEdited("- {}\n- b\n", tree);
    }

    [TestMethod]
    [DataRow("a:\n  - x\nb: 1\n", "a: []\nb: 1\n")]
    [DataRow("a:\n- x\n", "a: []\n")]
    public void RemoveAt_OnlyItemOfNestedList_WritesAnEmptyListOnTheKeyLine(string yaml, string expected)
    {
        var tree = YamlEditableTree.Parse(yaml);

        ((YamlEditableSequence)RootOf(tree).GetValue("a")!).RemoveAt(0);

        AssertEdited(expected, tree);
    }

    [TestMethod]
    public void RemoveAt_OnlyItemOfListInListItem_WritesAnEmptyListAfterTheDash()
    {
        var tree = YamlEditableTree.Parse("- - a\n- b\n");

        ((YamlEditableSequence)((YamlEditableSequence)tree.Root).Items[0].Value).RemoveAt(0);

        AssertEdited("- []\n- b\n", tree);
    }

    [TestMethod]
    [DataRow("a: {}\nb: 1\n", "a:\n  x: 1\nb: 1\n")]
    [DataRow("a: {} # one\n", "a: # one\n  x: 1\n")]
    [DataRow("a:\n  b: 1\nc: 2\n", "a:\n  x: 1\nc: 2\n")]
    public void SetValue_NewKeyInEmptyNestedMap_WritesTheMapAsABlock(string yaml, string expected)
    {
        var tree = YamlEditableTree.Parse(yaml);
        var map = (YamlEditableMapping)RootOf(tree).GetValue("a")!;
        map.RemoveEntry("b");

        map.SetValue("x", new YamlEditableScalar("1"));

        AssertEdited(expected, tree);
    }

    [TestMethod]
    public void AddEntry_EmptyNestedList_WritesTheListAsABlock()
    {
        var tree = YamlEditableTree.Parse("a: []\nb: 1\n");

        ((YamlEditableSequence)RootOf(tree).GetValue("a")!).AddEntry(new YamlEditableSequenceEntry(new YamlEditableScalar("x"), "  "));

        AssertEdited("a:\n  - x\nb: 1\n", tree);
    }

    [TestMethod]
    public void AddEntry_EmptyListInListItem_WritesTheItemAfterTheDash()
    {
        var tree = YamlEditableTree.Parse("- []\n- b\n");

        ((YamlEditableSequence)((YamlEditableSequence)tree.Root).Items[0].Value).AddEntry(new YamlEditableSequenceEntry(new YamlEditableScalar("x"), ""));

        AssertEdited("- - x\n- b\n", tree);
    }

    [TestMethod]
    public void SetValue_ListItemScalarToMap_KeepsTheCommentOnTheDashLine()
    {
        var tree = YamlEditableTree.Parse("- a # one\n- b\n");
        var map = new YamlEditableMapping();
        map.SetValue("x", new YamlEditableScalar("1"));
        map.SetValue("y", new YamlEditableScalar("2"));

        ((YamlEditableSequence)tree.Root).Items[0].Value = map;

        AssertEdited("- # one\n  x: 1\n  y: 2\n- b\n", tree);
    }

    [TestMethod]
    public void SetValue_NewKeyInListItemStartingOnTheNextLine_AlignsUnderTheFirstKey()
    {
        var tree = YamlEditableTree.Parse("- # one\n  a: 1\n");

        ((YamlEditableMapping)((YamlEditableSequence)tree.Root).Items[0].Value).SetValue("b", new YamlEditableScalar("2"));

        AssertEdited("- # one\n  a: 1\n  b: 2\n", tree);
    }

    [TestMethod]
    public void InsertEntry_AtStartOfListItemStartingOnTheNextLine_AlignsUnderTheFirstKey()
    {
        var tree = YamlEditableTree.Parse("-\n  a: 1\n");

        ((YamlEditableMapping)((YamlEditableSequence)tree.Root).Items[0].Value).InsertEntry(0, new YamlEditableMappingEntry("z", new YamlEditableScalar("0"), ""));

        AssertEdited("-\n  z: 0\n  a: 1\n", tree);
    }

    [TestMethod]
    public void SetValue_EmptyFlowMapAtTheRoot_WritesTheMapAsABlock()
    {
        var tree = YamlEditableTree.Parse("{}\n");

        RootOf(tree).SetValue("a", new YamlEditableScalar("1"));

        AssertEdited("a: 1\n", tree);
    }

    [TestMethod]
    [DataRow("? a\n: b\n")]
    [DataRow("? a\n")]
    [DataRow("x: 1\n? a\n")]
    [DataRow("? - a\n")]
    public void Parse_ExplicitKey_ThrowsNotSupportedException(string yaml)
    {
        Assert.ThrowsExactly<NotSupportedException>(() => YamlEditableTree.Parse(yaml));
    }

    [TestMethod]
    [DataRow("a: |+\n  x\n\nb: 1\n")]
    [DataRow("a: >2+\n  x\n\n")]
    [DataRow("a: |2\n    x\n")]
    public void Parse_BlockScalarWithKeepOrIndentationIndicator_ThrowsNotSupportedException(string yaml)
    {
        Assert.ThrowsExactly<NotSupportedException>(() => YamlEditableTree.Parse(yaml));
    }

    [TestMethod]
    [DataRow("|2-\n    hi")]
    [DataRow("|+\n  hi\n")]
    public void Edit_BlockScalarWithKeepOrIndentationIndicator_ThrowsAndChangesNothing(string text)
    {
        var tree = YamlEditableTree.Parse("a: 1\nb: 2\n");
        var root = RootOf(tree);
        var scalar = new YamlEditableScalar(text);

        Assert.ThrowsExactly<InvalidOperationException>(() => root.SetValue("a", scalar));
        Assert.ThrowsExactly<InvalidOperationException>(() => new YamlEditableMappingEntry("c", scalar, ""));
        Assert.ThrowsExactly<InvalidOperationException>(() => ((YamlEditableScalar)root.GetValue("b")!).Text = text);

        Assert.IsNull(scalar.Parent);
        Assert.AreSame(tree.SyntaxTree, tree.ToSyntaxTree());
    }

    [TestMethod]
    [DataRow("a: 1\n", "{}\n")]
    [DataRow("# one\na: 1", "{}")]
    public void RemoveEntry_LastKeyOfTheRoot_WritesAnEmptyMap(string yaml, string expected)
    {
        var tree = YamlEditableTree.Parse(yaml);

        RootOf(tree).RemoveEntry("a");

        AssertEdited(expected, tree);
    }

    [TestMethod]
    public void RemoveAt_LastItemOfTheRoot_WritesAnEmptyListThatTakesNewItems()
    {
        var tree = YamlEditableTree.Parse("- a\r\n");
        var root = (YamlEditableSequence)tree.Root;

        root.RemoveAt(0);
        AssertEdited("[]\r\n", tree);

        root.AddEntry(new YamlEditableSequenceEntry(new YamlEditableScalar("b"), ""));
        AssertEdited("- b\r\n", tree);
    }

    [TestMethod]
    public void SetValue_EmptyFlowMapAtTheRootAfterAComment_ReplacesTheBraces()
    {
        var tree = YamlEditableTree.Parse("# one\n{}\n");

        RootOf(tree).SetValue("a", new YamlEditableScalar("1"));
        AssertEdited("# one\n\na: 1\n", tree);

        RootOf(tree).RemoveEntry("a");
        AssertEdited("# one\n{}\n", tree);
    }

    [TestMethod]
    public void GetValueAndSetValue_KeysWithQuotesInTheirValue_FindTheEntryWithThatValue()
    {
        var tree = YamlEditableTree.Parse("\"name\": 1\n'\"name\"': 2\n");
        var root = RootOf(tree);

        Assert.AreEqual("1", ((YamlEditableScalar)root.GetValue("name")!).Text);
        Assert.AreEqual("2", ((YamlEditableScalar)root.GetValue("\"name\"")!).Text);

        root.SetValue("\"name\"", new YamlEditableScalar("3"));

        AssertEdited("\"name\": 1\n'\"name\"': 3\n", tree);
    }

    [TestMethod]
    public void SetValue_ExistingEmptyKey_ReplacesItsValue()
    {
        var tree = YamlEditableTree.Parse("\"\": 1\n");

        RootOf(tree).SetValue("", new YamlEditableScalar("2"));

        AssertEdited("\"\": 2\n", tree);
    }

    [TestMethod]
    [DataRow("\"name\"")]
    [DataRow("")]
    [DataRow("a: b")]
    [DataRow("# a")]
    [DataRow(" a")]
    [DataRow("- a")]
    [DataRow("? a")]
    public void SetValue_NewKeyThatIsNotAPlainScalar_ThrowsArgumentException(string key)
    {
        var tree = YamlEditableTree.Parse("name: 1\n");

        Assert.ThrowsExactly<ArgumentException>(() => RootOf(tree).SetValue(key, new YamlEditableScalar("2")));
        Assert.AreSame(tree.SyntaxTree, tree.ToSyntaxTree());
    }

    [TestMethod]
    public void AddEntry_KeyThatTheMapAlreadyHas_ThrowsArgumentException()
    {
        var root = RootOf(YamlEditableTree.Parse("name: 1\n"));

        Assert.ThrowsExactly<ArgumentException>(() => root.AddEntry(new YamlEditableMappingEntry("'name'", new YamlEditableScalar("2"), "")));
        Assert.ThrowsExactly<ArgumentException>(() => root.InsertEntry(0, new YamlEditableMappingEntry("\"name\"", new YamlEditableScalar("2"), "")));
    }

    [TestMethod]
    public void AddEntry_ToNestedListInListItem_AlignsTheNewItem()
    {
        var tree = YamlEditableTree.Parse("k:\n  - - a\n    - b\n");
        var inner = (YamlEditableSequence)((YamlEditableSequence)RootOf(tree).GetValue("k")!).Items[0].Value;

        inner.AddEntry(new YamlEditableSequenceEntry(new YamlEditableScalar("c"), "    "));

        AssertEdited("k:\n  - - a\n    - b\n    - c\n", tree);
    }

    [TestMethod]
    public void SetValue_ListItemToNewList_IndentsTheItemsOfTheNewList()
    {
        var tree = YamlEditableTree.Parse("- old\n- next\n");
        var list = new YamlEditableSequence();
        list.AddEntry(new YamlEditableSequenceEntry(new YamlEditableScalar("a"), ""));
        list.AddEntry(new YamlEditableSequenceEntry(new YamlEditableScalar("b"), ""));

        ((YamlEditableSequence)tree.Root).Items[0].Value = list;

        AssertEdited("- - a\n  - b\n- next\n", tree);
    }

    [TestMethod]
    public void SetValue_ValueAfterCommentInDocumentWithMixedLineBreaks_KeepsTheLineBreakAfterTheComment()
    {
        var tree = YamlEditableTree.Parse("a: # keep\r\n  x\nb: 2\n");

        RootOf(tree).SetValue("a", new YamlEditableScalar("y"));

        AssertEdited("a: # keep\r\n  y\nb: 2\n", tree);
    }

    [TestMethod]
    public void AddEntryAndValue_NodeUnderItself_ThrowInvalidOperationException()
    {
        var list = new YamlEditableSequence();
        Assert.ThrowsExactly<InvalidOperationException>(() => list.AddEntry(new YamlEditableSequenceEntry(list, "")));

        var map = new YamlEditableMapping();
        Assert.ThrowsExactly<InvalidOperationException>(() => map.AddEntry(new YamlEditableMappingEntry("k", map, "")));

        var root = RootOf(YamlEditableTree.Parse("a: 1\n"));
        Assert.ThrowsExactly<InvalidOperationException>(() => root.Entries[0].Value = root);
        Assert.AreEqual("a: 1\n", root.ToString());
    }

    [TestMethod]
    public void InsertEntry_AtStartOfListInListItem_TakesTheDashLine()
    {
        var tree = YamlEditableTree.Parse("- - a\n  - b\n- c\n");
        var inner = (YamlEditableSequence)((YamlEditableSequence)tree.Root).Items[0].Value;

        inner.InsertEntry(0, new YamlEditableSequenceEntry(new YamlEditableScalar("z"), "  "));

        AssertEdited("- - z\n  - a\n  - b\n- c\n", tree);
    }

    [TestMethod]
    [DataRow("key:\n  value", "key:\n  value\nb: 2")]
    [DataRow("key: # one\r\n  value", "key: # one\r\n  value\r\nb: 2")]
    public void SetValue_NewKeyAfterValueOnTheNextLineWithoutFinalNewLine_EndsTheValueLine(string yaml, string expected)
    {
        var tree = YamlEditableTree.Parse(yaml);

        RootOf(tree).SetValue("b", new YamlEditableScalar("2"));

        AssertEdited(expected, tree);
    }

    [TestMethod]
    [DataRow("k: \"a#b\"  # keep\n")]
    [DataRow("color: '#fff' # white\n")]
    [DataRow("a : b\n")]
    public void Parse_DocumentThatTheTreeWouldNotWriteBackExactly_ThrowsNotSupportedException(string yaml)
    {
        Assert.ThrowsExactly<NotSupportedException>(() => YamlEditableTree.Parse(yaml));
    }

    [TestMethod]
    public void AddEntry_AfterListItemOnTheNextLineWithoutFinalNewLine_EndsTheItemLine()
    {
        var tree = YamlEditableTree.Parse("-\n  old");

        ((YamlEditableSequence)tree.Root).AddEntry(new YamlEditableSequenceEntry(new YamlEditableScalar("new"), ""));

        AssertEdited("-\n  old\n- new", tree);
    }

    [TestMethod]
    public void RemoveAt_FirstItemOfListInListItem_MovesTheNextItemToTheDashLine()
    {
        var tree = YamlEditableTree.Parse("- - a\n  - b\n  - c\n- tail\n");

        ((YamlEditableSequence)((YamlEditableSequence)tree.Root).Items[0].Value).RemoveAt(0);

        AssertEdited("- - b\n  - c\n- tail\n", tree);
    }

    [TestMethod]
    public void SetValue_ListInListInListItem_IndentsTheItemsAfterTheInnerDash()
    {
        var tree = YamlEditableTree.Parse("- - x\n- tail\n");
        var list = new YamlEditableSequence();
        list.AddEntry(new YamlEditableSequenceEntry(new YamlEditableScalar("a"), ""));
        list.AddEntry(new YamlEditableSequenceEntry(new YamlEditableScalar("b"), ""));

        ((YamlEditableSequence)((YamlEditableSequence)tree.Root).Items[0].Value).Items[0].Value = list;

        AssertEdited("- - - a\n    - b\n- tail\n", tree);
    }

    [TestMethod]
    [DataRow("a: |\n  hi")]
    [DataRow("a: >\n  hi")]
    [DataRow("x:\n  a: |\n    hi")]
    public void SetValue_NewKeyAfterBlockScalarEndingTheDocumentWithoutLineBreak_ThrowsAndChangesNothing(string yaml)
    {
        var tree = YamlEditableTree.Parse(yaml);

        Assert.ThrowsExactly<InvalidOperationException>(() => RootOf(tree).SetValue("b", new YamlEditableScalar("2")));
        Assert.AreEqual(yaml, tree.Serialize());
    }

    [TestMethod]
    public void SetValue_NewKeyAfterStripBlockScalarEndingTheDocument_EndsItsLine()
    {
        var tree = YamlEditableTree.Parse("a: |-\r\n  hi");

        RootOf(tree).SetValue("b", new YamlEditableScalar("2"));

        AssertEdited("a: |-\r\n  hi\r\nb: 2", tree);
    }

    [TestMethod]
    [DataRow("a: old # keep", "a: # keep\n  x: 1")]
    [DataRow("a: # keep\n  {}\nb: 2\n", "a: # keep\n  x: 1\nb: 2\n")]
    [DataRow("a: # keep\n  {} # two\nb: 2\n", "a: # keep # two\n  x: 1\nb: 2\n")]
    // A comment line above the block would be read as the comment of its first entry.
    [DataRow("a: # keep\n  # between\n  {}\nb: 2\n", "a: # keep # between\n  x: 1\nb: 2\n")]
    public void SetValue_ValueBecomesABlock_KeepsTheComments(string yaml, string expected)
    {
        var tree = YamlEditableTree.Parse(yaml);
        var root = RootOf(tree);

        if (root.GetValue("a") is YamlEditableMapping empty)
        {
            empty.SetValue("x", new YamlEditableScalar("1"));
        }
        else
        {
            var map = new YamlEditableMapping();
            map.SetValue("x", new YamlEditableScalar("1"));
            root.SetValue("a", map);
        }

        AssertEdited(expected, tree);
    }

    [TestMethod]
    public void SetValue_NewMapWithBlockScalarAsValue_IndentsTheLinesOfTheScalar()
    {
        var tree = YamlEditableTree.Parse("c: 1\n");
        var map = new YamlEditableMapping();
        map.SetValue("x", new YamlEditableScalar("|\n  line\n\n  end"));

        RootOf(tree).SetValue("c", map);

        AssertEdited("c:\n  x: |\n    line\n\n    end\n", tree);
    }

    [TestMethod]
    public void SetValue_ListItemToMapWithBlockScalar_IndentsTheLinesOfTheScalar()
    {
        var tree = YamlEditableTree.Parse("- a\n");
        var map = new YamlEditableMapping();
        map.SetValue("x", new YamlEditableScalar("|\n  line"));
        map.SetValue("y", new YamlEditableScalar("1"));

        ((YamlEditableSequence)tree.Root).Items[0].Value = map;

        AssertEdited("- x: |\n    line\n  y: 1\n", tree);
    }

    [TestMethod]
    [DataRow("a: | # keep\n  hi\nb: 2\n", "changed", "a: changed # keep\nb: 2\n", "changed")]
    [DataRow("a: | # keep\n  hi\nb: 2\n", "|-\n  new", "a: |- # keep\n  new\nb: 2\n", "new")]
    [DataRow("a: x # keep\nb: 2\n", "|\n  hi", "a: | # keep\n  hi\nb: 2\n", "hi\n")]
    // The value of ">" ends with the line break of its last line, also at the end of the document.
    [DataRow("a: x # keep", ">\n  hi", "a: > # keep\n  hi\n", "hi\n")]
    public void SetValue_BlockScalarReplacedOrSet_KeepsTheCommentOnTheLineOfTheKey(string yaml, string text, string expected, string expectedValue)
    {
        var tree = YamlEditableTree.Parse(yaml);

        RootOf(tree).SetValue("a", new YamlEditableScalar(text));

        AssertEdited(expected, tree);
        Assert.AreEqual(expectedValue, YamlSerializer.Deserialize<Dictionary<string, string>>(tree.Serialize())!["a"]);
    }

    [TestMethod]
    public void SetValue_ListItemToOneKeyMapWithBlockScalar_IndentsTheLinesOfTheScalar()
    {
        var tree = YamlEditableTree.Parse("- old\n- tail\n");
        var map = new YamlEditableMapping();
        map.SetValue("x", new YamlEditableScalar("|\n  y: 1"));

        ((YamlEditableSequence)tree.Root).Items[0].Value = map;

        AssertEdited("- x: |\n    y: 1\n- tail\n", tree);
        Assert.AreEqual("y: 1\n", YamlSerializer.Deserialize<List<Dictionary<string, string>>>("- x: |\n    y: 1\n")![0]["x"]);
    }

    [TestMethod]
    [DataRow("hello # keep", "| # keep\n  hi")]
    [DataRow("hello # keep\n# end\n", "| # keep\n  hi\n# end\n")]
    public void Text_RootScalarBecomesBlockScalar_MovesTheCommentToItsHeader(string yaml, string expected)
    {
        var tree = YamlEditableTree.Parse(yaml);

        ((YamlEditableScalar)tree.Root).Text = "|\n  hi";

        AssertEdited(expected, tree);
        Assert.ThrowsExactly<InvalidOperationException>(() => ((YamlEditableScalar)tree.Root).Text = "|+\n  hi");
    }

    [TestMethod]
    public void SetValue_ListItemToNestedMapWithBlockScalar_KeepsTheCommentOnTheDashLine()
    {
        var tree = YamlEditableTree.Parse("- old      # keep\n- tail\n");
        var inner = new YamlEditableMapping();
        inner.SetValue("z", new YamlEditableScalar("|-\n  hi"));
        var outer = new YamlEditableMapping();
        outer.SetValue("x", inner);

        ((YamlEditableSequence)tree.Root).Items[0].Value = outer;

        AssertEdited("- # keep\n  x:\n    z: |-\n      hi\n- tail\n", tree);
    }

    [TestMethod]
    [DataRow("a: |- # keep\n  hi\nb: 2\n", "changed", "a: changed # keep\nb: 2\n")]
    [DataRow("- |- # keep\n  hi\n- b\n", "x", "- x # keep\n- b\n")]
    [DataRow("| # keep\n  hi\n", "plain", "plain # keep\n")]
    [DataRow("a: | # keep\n  hi\nb: 2\n", ">-\n  new", "a: >- # keep\n  new\nb: 2\n")]
    public void Text_BlockScalarReplaced_KeepsItsHeaderComment(string yaml, string text, string expected)
    {
        var tree = YamlEditableTree.Parse(yaml);
        var scalar = tree.Root switch
        {
            YamlEditableMapping map => (YamlEditableScalar)map.Entries[0].Value,
            YamlEditableSequence list => (YamlEditableScalar)list.Items[0].Value,
            _ => (YamlEditableScalar)tree.Root,
        };

        scalar.Text = text;

        AssertEdited(expected, tree);
    }

    [TestMethod]
    public void SetValue_KeepIndicatorAfterSpaces_ThrowsInvalidOperationException()
    {
        var tree = YamlEditableTree.Parse("a: old\nb: 2\n");

        Assert.ThrowsExactly<InvalidOperationException>(() => RootOf(tree).SetValue("a", new YamlEditableScalar(" |+\n  hi")));
        Assert.AreSame(tree.SyntaxTree, tree.ToSyntaxTree());
    }

    [TestMethod]
    public void ToSyntaxTree_CommentLineThatWouldJoinABlockScalar_ThrowsAndKeepsTheSnapshot()
    {
        var tree = YamlEditableTree.Parse("a: |\n  hi\nb: 1\n    # deep\nc: 2\n");
        var snapshot = tree.SyntaxTree;

        RootOf(tree).RemoveEntry("b");

        Assert.ThrowsExactly<InvalidOperationException>(() => tree.ToSyntaxTree());
        Assert.AreSame(snapshot, tree.SyntaxTree);
    }

    [TestMethod]
    [DataRow("a: v\n", "|\nx: 1", "a: |\n  x: 1\n")]
    [DataRow("a:\n  b: v\n", "|\n x: 1\n", "a:\n  b: |\n    x: 1\n")]
    [DataRow("- v\n", "\"one\ntwo\"", "- \"one\n  two\"\n")]
    [DataRow("a: v", "|\n  x\n", "a: |\n  x\n")]
    public void Text_ScalarWhoseLinesAreNotUnderItsEntry_IsIndentedUnderIt(string yaml, string text, string expected)
    {
        var tree = YamlEditableTree.Parse(yaml);
        var scalar = Scalars(tree.Root)[0];

        scalar.Text = text;

        AssertEdited(expected, tree);
    }

    [TestMethod]
    [DataRow("one: 1\nnested:\n  two: 2\n", 0)]
    [DataRow("nested:\n  two: 2\none: 1\n", 1)]
    public void AddEntry_EntryMovedToANestedMap_TakesTheColumnOfItsEntries(string yaml, int index)
    {
        var tree = YamlEditableTree.Parse(yaml);
        var root = RootOf(tree);
        var entry = root.Entries[index];
        root.RemoveEntry("one");

        ((YamlEditableMapping)root.GetValue("nested")!).AddEntry(entry);

        AssertEdited("nested:\n  two: 2\n  one: 1\n", tree);
    }

    [TestMethod]
    public void ToSyntaxTree_SpacesLineThatWouldJoinABlockScalar_ThrowsInvalidOperationException()
    {
        var tree = YamlEditableTree.Parse("a: |-\n  hi\nb: 1\n    \nc: 2\n");

        RootOf(tree).RemoveEntry("b");

        Assert.ThrowsExactly<InvalidOperationException>(() => tree.ToSyntaxTree());
    }

    [TestMethod]
    public void Text_BlockScalarSetToItsOwnText_ChangesNothing()
    {
        var tree = YamlEditableTree.Parse("a: |- # keep\n  hi\n");
        var scalar = (YamlEditableScalar)RootOf(tree).GetValue("a")!;

        scalar.Text = scalar.Text;

        Assert.AreEqual("a: |- # keep\n  hi\n", tree.Serialize());
        Assert.AreSame(tree.SyntaxTree, tree.ToSyntaxTree());
    }

    [TestMethod]
    [DataRow("y ", "a: y \n")]
    [DataRow("\n  y", "a: \n  y\n")]
    public void Text_WithSpacesAroundTheScalar_IsAccepted(string text, string expected)
    {
        var tree = YamlEditableTree.Parse("a: x\n");

        ((YamlEditableScalar)RootOf(tree).GetValue("a")!).Text = text;

        AssertEdited(expected, tree);
    }

    [TestMethod]
    public void ToSyntaxTree_ScalarTextWithAFlowCollection_ThrowsInvalidOperationException()
    {
        var tree = YamlEditableTree.Parse("a: x\n");

        ((YamlEditableScalar)RootOf(tree).GetValue("a")!).Text = "[1, 2]";

        Assert.ThrowsExactly<InvalidOperationException>(() => tree.ToSyntaxTree());
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void ToSyntaxTree_BlockScalarMovedFromTheEndOfTheDocument_ThrowsBecauseItsValueWouldChange(bool setItsOwnText)
    {
        var tree = YamlEditableTree.Parse("- a\n- |\n  hi");
        var list = (YamlEditableSequence)tree.Root;
        var item = list.Items[1];
        if (setItsOwnText)
        {
            var scalar = (YamlEditableScalar)item.Value;
            scalar.Text = scalar.Text;
        }

        list.RemoveAt(1);
        list.InsertEntry(0, item);

        Assert.ThrowsExactly<InvalidOperationException>(() => tree.ToSyntaxTree());
    }

    [TestMethod]
    public void ToSyntaxTree_TriviaEditThatChangesTheValueOfABlockScalar_ThrowsInvalidOperationException()
    {
        var tree = YamlEditableTree.Parse("a: |\n  hi\n");

        RootOf(tree).Entries[0].EndOfLineTrivia!.Clear();

        Assert.ThrowsExactly<InvalidOperationException>(() => tree.ToSyntaxTree());
    }

    [TestMethod]
    [DataRow("!!str |+\n  hi")]
    [DataRow("&label |2\n  hi")]
    [DataRow("!!str old")]
    public void ToSyntaxTree_ScalarTextWithATagOrAnAnchor_ThrowsInvalidOperationException(string text)
    {
        var tree = YamlEditableTree.Parse("a: old");

        ((YamlEditableScalar)RootOf(tree).GetValue("a")!).Text = text;

        Assert.ThrowsExactly<InvalidOperationException>(() => tree.ToSyntaxTree());
    }

    [TestMethod]
    [DataRow("- a: 1\n  # about b\n  b: 2\n", "- {}\n")]
    [DataRow("- - a\n  # about b\n  - b\n", "- []\n")]
    public void RemoveFirstThenReopenAndRemoveNext_RemovesTheCommentOfTheNext(string yaml, string expected)
    {
        var tree = YamlEditableTree.Parse(yaml);
        RemoveFirstOfTheItem(tree);
        var reopened = YamlEditableTree.Parse(tree.ToSyntaxTree().ToFullString());

        RemoveFirstOfTheItem(reopened);

        AssertEdited(expected, reopened);

        static void RemoveFirstOfTheItem(YamlEditableTree tree)
        {
            switch (((YamlEditableSequence)tree.Root).Items[0].Value)
            {
                case YamlEditableMapping map:
                    map.RemoveEntry(map.Entries[0].KeyText);
                    break;
                case YamlEditableSequence list:
                    list.RemoveAt(0);
                    break;
            }
        }
    }

    [TestMethod]
    public void Text_RejectedText_KeepsTheValueCheckOfTheScalar()
    {
        var tree = YamlEditableTree.Parse("- a\n- |\n  hi");
        var list = (YamlEditableSequence)tree.Root;
        var item = list.Items[1];

        Assert.ThrowsExactly<InvalidOperationException>(() => ((YamlEditableScalar)item.Value).Text = "|+\n  hi");
        list.RemoveAt(1);
        list.InsertEntry(0, item);

        Assert.ThrowsExactly<InvalidOperationException>(() => tree.ToSyntaxTree());
    }

    [TestMethod]
    [DataRow("-\n  # c\n  {}\n", "-\n  # c\n  {}\n")]
    [DataRow("- # c\n  {}\n", "- # c\n  {}\n")]
    [DataRow("-\n  # c\n  []\n", "-\n  # c\n  []\n")]
    [DataRow("- # dash\n  # keep\n  {}\n- tail\n", "- # dash\n  # keep\n  {}\n- tail\n")]
    public void AddThenRemove_EmptyCollectionOfListItemAfterComments_KeepsTheComments(string yaml, string expected)
    {
        var tree = YamlEditableTree.Parse(yaml);
        switch (((YamlEditableSequence)tree.Root).Items[0].Value)
        {
            case YamlEditableMapping map:
                map.SetValue("x", new YamlEditableScalar("1"));
                map.RemoveEntry("x");
                break;
            case YamlEditableSequence list:
                list.AddEntry(new YamlEditableSequenceEntry(new YamlEditableScalar("1"), ""));
                list.RemoveAt(0);
                break;
        }

        AssertEdited(expected, tree);
    }

    [TestMethod]
    public void SetValueThenRemoveEntry_EmptyRootWithComment_KeepsTheCommentOfTheRoot()
    {
        var tree = YamlEditableTree.Parse("{} # keep\n");
        var nested = new YamlEditableMapping();
        nested.SetValue("x", new YamlEditableScalar("1"));
        nested.SetValue("y", new YamlEditableScalar("2"));

        RootOf(tree).SetValue("a", nested);
        RootOf(tree).SetValue("b", new YamlEditableScalar("2"));
        AssertEdited("# keep\n\na:\n  x: 1\n  y: 2\nb: 2\n", tree);

        var reopened = YamlEditableTree.Parse(tree.Serialize());
        RootOf(reopened).RemoveEntry("a");
        RootOf(reopened).RemoveEntry("b");
        AssertEdited("# keep\n\n{}\n", reopened);

        RootOf(tree).RemoveEntry("a");
        RootOf(tree).RemoveEntry("b");
        AssertEdited("{} # keep\n", tree);
    }

    [TestMethod]
    // Removing the entry again gives back the text; read again, the empty map is written on the line of its owner.
    [DataRow("- # dash\n  {} # keep\n- tail\n", "- # dash # keep\n  x: 1\n- tail\n", "- # dash\n  {} # keep\n- tail\n", "- {} # dash # keep\n- tail\n")]
    [DataRow("- {} # keep\n", "- # keep\n  x: 1\n", "- {} # keep\n", "- {} # keep\n")]
    [DataRow("-     {} # keep\n- tail\n", "- # keep\n  x: 1\n- tail\n", "-     {} # keep\n- tail\n", "- {} # keep\n- tail\n")]
    [DataRow("a:\n  {} # keep\nb: 2\n", "a: # keep\n  x: 1\nb: 2\n", "a:\n  {} # keep\nb: 2\n", "a: {} # keep\nb: 2\n")]
    // Read again, the blank line above the key is the key's, and goes with it.
    [DataRow("-\n\n  {}\n- tail\n", "-\n\n  x: 1\n- tail\n", "-\n\n  {}\n- tail\n", "- {}\n- tail\n")]
    public void AddThenRemove_CommentOnTheLineOfAnEmptyMap_StaysWithItsOwner(string yaml, string added, string removed, string removedAfterReopen)
    {
        var tree = YamlEditableTree.Parse(yaml);
        GetMap(tree).SetValue("x", new YamlEditableScalar("1"));
        AssertEdited(added, tree);

        var reopened = YamlEditableTree.Parse(added);
        GetMap(reopened).RemoveEntry("x");
        AssertEdited(removedAfterReopen, reopened);

        GetMap(tree).RemoveEntry("x");
        AssertEdited(removed, tree);

        static YamlEditableMapping GetMap(YamlEditableTree tree) =>
            tree.Root is YamlEditableSequence list ? (YamlEditableMapping)list.Items[0].Value : (YamlEditableMapping)((YamlEditableMapping)tree.Root).GetValue("a")!;
    }

    [TestMethod]
    public void SetValue_BlockScalarWithTheSameHeaderComment_DoesNotRepeatIt()
    {
        var tree = YamlEditableTree.Parse("a: | # keep\n  hi\nb: 2\n");

        RootOf(tree).SetValue("a", new YamlEditableScalar("| # keep\n  hi"));

        AssertEdited("a: | # keep\n  hi\nb: 2\n", tree);
    }

    [TestMethod]
    public void SetValueThenRemoveEntry_IndentedKeyInIndentedEmptyRoot_KeepsTheIndentation()
    {
        var tree = YamlEditableTree.Parse("  { }\n");

        RootOf(tree).SetValue("a", new YamlEditableScalar("1"), "  ");
        RootOf(tree).RemoveEntry("a");

        Assert.AreEqual("  { }\n", tree.Serialize());
    }

    [TestMethod]
    public void RemoveEntry_FirstThenNextKeyOfListItemBeforeCommentLines_LeavesAnEmptyMap()
    {
        var tree = YamlEditableTree.Parse("- a: 1\n  # keep\n  b: 2\n");
        var map = (YamlEditableMapping)((YamlEditableSequence)tree.Root).Items[0].Value;

        map.RemoveEntry("a");
        map.RemoveEntry("b");

        AssertEdited("- {}\n", tree);
    }

    [TestMethod]
    public void SetValue_ListItemWithCommentWithoutFinalNewLineToMap_KeepsTheCommentOnTheDashLine()
    {
        var tree = YamlEditableTree.Parse("- old # keep");
        var map = new YamlEditableMapping();
        map.SetValue("x", new YamlEditableScalar("1"));
        map.SetValue("y", new YamlEditableScalar("2"));

        ((YamlEditableSequence)tree.Root).Items[0].Value = map;

        AssertEdited("- # keep\n  x: 1\n  y: 2", tree);
    }

    [TestMethod]
    public void RemoveEntry_OnlyKeyOfIndentedEmptyRoot_KeepsTheIndentation()
    {
        var tree = YamlEditableTree.Parse("  { }\n");

        RootOf(tree).SetValue("a", new YamlEditableScalar("1"));
        RootOf(tree).RemoveEntry("a");

        Assert.AreEqual("  { }\n", tree.Serialize());
        Assert.AreSame(tree.SyntaxTree, tree.ToSyntaxTree());
    }

    [TestMethod]
    [DataRow("\n  |+\n    hi")]
    [DataRow("\r\n  |2\r\n    hi")]
    public void Text_UnsupportedBlockScalarAfterALineBreak_ThrowsInvalidOperationException(string text)
    {
        var tree = YamlEditableTree.Parse("a: old");

        Assert.ThrowsExactly<InvalidOperationException>(() => ((YamlEditableScalar)RootOf(tree).GetValue("a")!).Text = text);
        Assert.AreEqual("a: old", tree.Serialize());
    }

    [TestMethod]
    [DataRow("a: first\n'a': second\n")]
    [DataRow("- x: 1\n  \"x\": 2\n")]
    public void Parse_MappingWithTheSameKeyTwice_ThrowsNotSupportedException(string yaml)
    {
        Assert.ThrowsExactly<NotSupportedException>(() => YamlEditableTree.Parse(yaml));
    }

    [TestMethod]
    [DataRow("- a: 1\n  # keep\n  b: 2\n", "-\n  # keep\n  b: 2\n")]
    [DataRow("- a: 1\n\n  b: 2\n", "-\n\n  b: 2\n")]
    [DataRow("- a: 1\r\n  # keep\r\n  b: 2\r\n", "-\r\n  # keep\r\n  b: 2\r\n")]
    public void RemoveEntry_FirstKeyOfListItemBeforeCommentLines_LeavesTheDashAloneOnItsLine(string yaml, string expected)
    {
        var tree = YamlEditableTree.Parse(yaml);

        ((YamlEditableMapping)((YamlEditableSequence)tree.Root).Items[0].Value).RemoveEntry("a");

        AssertEdited(expected, tree);
    }

    [TestMethod]
    public void RemoveAt_FirstItemOfListInListItemBeforeCommentLines_LeavesTheDashAloneOnItsLine()
    {
        var tree = YamlEditableTree.Parse("- - a\n  # keep\n  - b\n");

        ((YamlEditableSequence)((YamlEditableSequence)tree.Root).Items[0].Value).RemoveAt(0);

        AssertEdited("-\n  # keep\n  - b\n", tree);
    }

    [TestMethod]
    public void EntriesAndItems_CastToAList_CannotBeChanged()
    {
        var root = RootOf(YamlEditableTree.Parse("a:\n  - x\n"));

        Assert.ThrowsExactly<NotSupportedException>(() => ((IList<YamlEditableMappingEntry>)root.Entries).Add(new YamlEditableMappingEntry("b", new YamlEditableScalar("1"), "")));
        Assert.ThrowsExactly<NotSupportedException>(() => ((IList<YamlEditableSequenceEntry>)((YamlEditableSequence)root.GetValue("a")!).Items).RemoveAt(0));
    }

    [TestMethod]
    public void Text_PlainScalarBecomesBlockScalar_MovesTheCommentToItsHeader()
    {
        var tree = YamlEditableTree.Parse("- a # keep\n- b\n");

        ((YamlEditableScalar)((YamlEditableSequence)tree.Root).Items[0].Value).Text = "|\n  hi";

        AssertEdited("- | # keep\n  hi\n- b\n", tree);
    }

    [TestMethod]
    public void SetValue_NewBlockMapInEmptyCrLfRoot_UsesCrLf()
    {
        var tree = YamlEditableTree.Parse("{}\r\n");
        var map = new YamlEditableMapping();
        map.SetValue("x", new YamlEditableScalar("1"));

        RootOf(tree).SetValue("a", map);

        AssertEdited("a:\r\n  x: 1\r\n", tree);
    }

    [TestMethod]
    public void AddEntry_EmptyMapWhoseNewLastLineHasAComment_KeepsTheOldCommentOnTheKeyLine()
    {
        var tree = YamlEditableTree.Parse("a: # keep\n  {} # two\nb: 2\n");
        var map = (YamlEditableMapping)RootOf(tree).GetValue("a")!;

        map.AddEntry(new YamlEditableMappingEntry("x", new YamlEditableScalar("1"), "")
        {
            EndOfLineTrivia = [new YamlEditableTrivia(YamlEditableTriviaKind.Whitespace, " "), new YamlEditableTrivia(YamlEditableTriviaKind.Comment, "# own")],
        });

        AssertEdited("a: # keep # two\n  x: 1 # own\nb: 2\n", tree);
    }

    [TestMethod]
    public void SetValue_RejectedAfterBlockScalarAtTheEnd_LeavesTheValueWithoutParent()
    {
        var tree = YamlEditableTree.Parse("a: |\n  hi");
        var value = new YamlEditableScalar("2");

        Assert.ThrowsExactly<InvalidOperationException>(() => RootOf(tree).SetValue("b", value));

        Assert.IsNull(value.Parent);
    }

    [TestMethod]
    public void AddEntryAndSetValue_KeyThatCannotBeAnImplicitKey_ThrowArgumentException()
    {
        var root = RootOf(YamlEditableTree.Parse("a: 1\n"));

        Assert.ThrowsExactly<ArgumentException>(() => root.AddEntry(new YamlEditableMappingEntry("\"line\n  two\"", new YamlEditableScalar("2"), "")));
        Assert.ThrowsExactly<ArgumentException>(() => root.SetValue(string.Concat(System.Linq.Enumerable.Repeat("\U0001F600", 513)), new YamlEditableScalar("2")));
    }

    [TestMethod]
    public void SetValue_ListItemToMapWithIndentedKeys_PutsTheFirstKeyAfterTheDash()
    {
        var tree = YamlEditableTree.Parse("- old\n- tail\n");
        var map = new YamlEditableMapping();
        map.SetValue("x", new YamlEditableScalar("1"), "  ");
        map.SetValue("y", new YamlEditableScalar("2"));

        ((YamlEditableSequence)tree.Root).Items[0].Value = map;

        AssertEdited("- x: 1\n  y: 2\n- tail\n", tree);
    }

    [TestMethod]
    public void SetValue_IndentedEmptyMapAtTheRootInCrLfDocument_UsesCrLf()
    {
        var tree = YamlEditableTree.Parse("  { }\r\n");

        RootOf(tree).SetValue("a", new YamlEditableScalar("1"));
        RootOf(tree).SetValue("b", new YamlEditableScalar("2"));

        AssertEdited("  a: 1\r\n  b: 2\r\n", tree);
    }

    [TestMethod]
    public void SetValue_IndentedEmptyMapAtTheRoot_KeepsTheIndentationForEveryKey()
    {
        var tree = YamlEditableTree.Parse("  {}\n");

        RootOf(tree).SetValue("a", new YamlEditableScalar("1"));
        RootOf(tree).SetValue("b", new YamlEditableScalar("2"));

        AssertEdited("  a: 1\n  b: 2\n", tree);
    }

    [TestMethod]
    public void SetValue_RootAsItsOwnValue_ThrowsAndChangesNothing()
    {
        var tree = YamlEditableTree.Parse("");
        var root = RootOf(tree);

        Assert.ThrowsExactly<InvalidOperationException>(() => root.SetValue("new", root));
        Assert.ThrowsExactly<InvalidOperationException>(() => root.AddEntry(new YamlEditableMappingEntry("k", root, "")));
        Assert.IsNull(root.Parent);
        Assert.AreEqual("", tree.Serialize());
    }

    [TestMethod]
    public void SetValue_KeyLongerThanAnImplicitKey_ThrowsArgumentException()
    {
        var root = RootOf(YamlEditableTree.Parse("a: 1\n"));

        root.SetValue(new string('k', 1024), new YamlEditableScalar("2"));
        Assert.ThrowsExactly<ArgumentException>(() => root.SetValue(new string('k', 1025), new YamlEditableScalar("3")));
    }

    [TestMethod]
    [DataRow("name ")]
    [DataRow("a: b")]
    [DataRow("'name' # c")]
    public void AddEntry_KeyTextThatIsNotOneScalarOrADuplicate_ThrowsArgumentException(string rawKey)
    {
        var root = RootOf(YamlEditableTree.Parse("name: 1\n"));

        Assert.ThrowsExactly<ArgumentException>(() => root.AddEntry(new YamlEditableMappingEntry(rawKey, new YamlEditableScalar("2"), "")));
    }

    [TestMethod]
    public void Key_RenamedToTheKeyOfAnotherEntry_ThrowsArgumentException()
    {
        var root = RootOf(YamlEditableTree.Parse("a: 1\nb: 2\n"));

        Assert.ThrowsExactly<ArgumentException>(() => root.Entries[1].KeyText = "'a'");
        root.Entries[1].KeyText = "\"c\"";
        Assert.AreEqual("2", ((YamlEditableScalar)root.GetValue("c")!).Text);
    }

    [TestMethod]
    public void GetValueAndRemoveEntry_NullKey_ThrowArgumentNullException()
    {
        var map = RootOf(YamlEditableTree.Parse("a: 1\n"));

        Assert.ThrowsExactly<ArgumentNullException>(() => map.GetValue(null!));
        Assert.ThrowsExactly<ArgumentNullException>(() => map.RemoveEntry(null!));
    }
    [TestMethod]
    [DataRow("- {} # keep\n- tail\n")]
    [DataRow("- [] # keep\n- tail\n")]
    public void SetValue_EmptyCollectionOfListItemReplacedByAMap_KeepsTheCommentWithTheItem(string yaml)
    {
        var tree = YamlEditableTree.Parse(yaml);
        var map = new YamlEditableMapping();
        map.SetValue("x", new YamlEditableScalar("1"));
        map.SetValue("y", new YamlEditableScalar("2"));

        ((YamlEditableSequence)tree.Root).Items[0].Value = map;
        AssertEdited("- # keep\n  x: 1\n  y: 2\n- tail\n", tree);

        var reopened = YamlEditableTree.Parse(tree.Serialize());
        ((YamlEditableMapping)((YamlEditableSequence)reopened.Root).Items[0].Value).RemoveEntry("x");
        AssertEdited("- # keep\n  y: 2\n- tail\n", reopened);

        map.RemoveEntry("x");
        AssertEdited("- # keep\n  y: 2\n- tail\n", tree);
    }

    [TestMethod]
    [DataRow("name: app", "name", "app")]
    [DataRow("name: \"a\\tb\"", "name", "a\tb")]
    [DataRow("name: 'it''s'", "name", "it's")]
    [DataRow("name: one\n  two\n", "name", "one two")]
    [DataRow("name: |\n  one\n  two\nnext: 1\n", "name", "one\ntwo\n")]
    [DataRow("name: >-\n  one\n  two\n", "name", "one two")]
    [DataRow("name:\nnext: 1\n", "name", "")]
    [DataRow("name: 1", "name", "1")]
    public void Value_ParsedScalar_IsTheStringItIsReadAs(string yaml, string key, string expected)
    {
        var tree = YamlEditableTree.Parse(yaml);

        Assert.AreEqual(expected, ((YamlEditableScalar)RootOf(tree).GetValue(key)!).Value);
    }

    [TestMethod]
    [DataRow("\"x\\ny\"", "x\ny")]
    [DataRow("|\n  block\n", "block\n")]
    [DataRow("plain", "plain")]
    [DataRow("", "")]
    public void Value_TextSetOrBuilt_IsTheStringItIsReadAs(string text, string expected)
    {
        var tree = YamlEditableTree.Parse("a: 1\n");
        var scalar = (YamlEditableScalar)RootOf(tree).GetValue("a")!;

        scalar.Text = text;

        Assert.AreEqual(expected, scalar.Value);
        Assert.AreEqual(expected, new YamlEditableScalar(text).Value);
    }

    [TestMethod]
    public void Value_TextThatIsNotOneScalar_Throws()
    {
        Assert.ThrowsExactly<InvalidOperationException>(() => new YamlEditableScalar("!!str x").Value);
        Assert.ThrowsExactly<InvalidOperationException>(() => new YamlEditableScalar("[1, 2]").Value);
        Assert.ThrowsExactly<InvalidOperationException>(() => new YamlEditableScalar("a: b").Value);
        Assert.Throws<YamlException>(() => new YamlEditableScalar("\"open").Value);
    }

    [TestMethod]
    [DataRow("# header\n\nname: app\nport: 80\n", "# header\n\nport: 80\n", "# header\n\n{}\n")]
    [DataRow("# about name\nname: app\nport: 80\n", "port: 80\n", "{}\n")]
    [DataRow("# header\n\n# about name\nname: app\nport: 80\n", "# header\n\nport: 80\n", "# header\n\n{}\n")]
    public void RemoveEntry_FirstKeyOfTheDocument_KeepsTheHeaderOfTheDocument(string yaml, string withoutName, string empty)
    {
        var tree = YamlEditableTree.Parse(yaml);

        RootOf(tree).RemoveEntry("name");
        AssertEdited(withoutName, tree);

        RootOf(tree).RemoveEntry("port");
        AssertEdited(empty, tree);
    }

    [TestMethod]
    public void RemoveThenInsert_FirstKeyBeforeASectionComment_GivesTheSectionCommentBack()
    {
        var tree = YamlEditableTree.Parse("a: 1\n\n# Section B\n\nb: 2\n");
        var a = RootOf(tree).Entries[0];

        RootOf(tree).RemoveEntry("a");
        AssertEdited("\n# Section B\n\nb: 2\n", tree);

        RootOf(tree).InsertEntry(0, a);
        AssertEdited("a: 1\n\n# Section B\n\nb: 2\n", tree);
    }

    [TestMethod]
    public void ToSyntaxTree_CommentMovedToAnotherEntryByItsTrivia_ThrowsAndKeepsTheSnapshot()
    {
        var tree = YamlEditableTree.Parse("a: 1\nb: 2\n");
        var snapshot = tree.SyntaxTree;

        RootOf(tree).Entries[0].EndOfLineTrivia!.AddRange(
        [
            new YamlEditableTrivia(YamlEditableTriviaKind.Comment, "# about a"),
            new YamlEditableTrivia(YamlEditableTriviaKind.NewLine, "\n"),
        ]);

        Assert.AreEqual("a: 1\n# about a\nb: 2\n", tree.Serialize());
        Assert.ThrowsExactly<InvalidOperationException>(() => tree.ToSyntaxTree());
        Assert.AreSame(snapshot, tree.SyntaxTree);
    }

    [TestMethod]
    [DataRow("a:\n  b: |\n    text\nc: 1\n", "a: {}\nc: 1\nb: |\n  text\n")]
    [DataRow("a:\n  b: \"one\n    two\"\nc: 1\n", "a: {}\nc: 1\nb: \"one\n  two\"\n")]
    [DataRow("a:\n  b:\n    - 1\n    - 2\nc: 1\n", "a: {}\nc: 1\nb:\n  - 1\n  - 2\n")]
    public void RemoveThenAddEntry_EntryMovedToAnotherDepth_MovesTheLinesOfItsValue(string yaml, string expected)
    {
        var tree = YamlEditableTree.Parse(yaml);
        var nested = (YamlEditableMapping)RootOf(tree).GetValue("a")!;
        var b = nested.Entries[0];
        var value = b.Value is YamlEditableScalar scalar ? scalar.Value : null;

        nested.RemoveEntry("b");
        RootOf(tree).AddEntry(b);

        AssertEdited(expected, tree);
        if (value is not null)
        {
            Assert.AreEqual(value, ((YamlEditableScalar)RootOf(YamlEditableTree.Parse(expected)).GetValue("b")!).Value);
        }
    }

    [TestMethod]
    [DataRow("key:\n  value # c\nlast: 1\n", "key: # c\nlast: 1\n")]
    [DataRow("key: value # c\nlast: 1\n", "key: # c\nlast: 1\n")]
    [DataRow("-\n  # c\n  {}\n- tail\n", "- # c\n- tail\n")]
    public void Text_SetToAMissingValue_WritesItAfterTheKeyAndKeepsTheComment(string yaml, string expected)
    {
        var tree = YamlEditableTree.Parse(yaml);
        var entry = tree.Root is YamlEditableMapping map ? (YamlEditableNode)map.Entries[0] : ((YamlEditableSequence)tree.Root).Items[0];

        switch (entry)
        {
            case YamlEditableMappingEntry mapEntry:
                ((YamlEditableScalar)mapEntry.Value).Text = string.Empty;
                break;
            case YamlEditableSequenceEntry listEntry:
                listEntry.Value = new YamlEditableScalar(string.Empty);
                break;
        }

        AssertEdited(expected, tree);
    }

    [TestMethod]
    [DataRow("a: 1\nb: 2", "a: 1")]
    [DataRow("a: 1 # c\nb: 2", "a: 1 # c")]
    [DataRow("a:\n  x: 1\nb: 2", "a:\n  x: 1")]
    public void RemoveEntry_LastLineOfDocumentWithoutFinalLineBreak_KeepsNoFinalLineBreak(string yaml, string expected)
    {
        var tree = YamlEditableTree.Parse(yaml);

        RootOf(tree).RemoveEntry("b");

        AssertEdited(expected, tree);
    }

    [TestMethod]
    public void AddEntry_EntriesBuiltWithoutIndentation_TakeTheColumnOfTheOtherEntries()
    {
        var tree = YamlEditableTree.Parse("a:\n  - x\nb:\n    c: 1\n");

        ((YamlEditableSequence)RootOf(tree).GetValue("a")!).AddEntry(new YamlEditableSequenceEntry(new YamlEditableScalar("y")));
        ((YamlEditableMapping)RootOf(tree).GetValue("b")!).InsertEntry(0, new YamlEditableMappingEntry("d", new YamlEditableScalar("2")));

        AssertEdited("a:\n  - x\n  - y\nb:\n    d: 2\n    c: 1\n", tree);
    }
    [TestMethod]
    public void ToSyntaxTree_NewScalarMovedAfterTheEdits_KeepsTheValueItWasWrittenWith()
    {
        var tree = YamlEditableTree.Parse("a: 1\nb: old");
        var root = RootOf(tree);
        root.SetValue("b", new YamlEditableScalar("|\n  x"));
        AssertEdited("a: 1\nb: |\n  x\n", tree);
        Assert.AreEqual("x\n", ((YamlEditableScalar)root.GetValue("b")!).Value);

        var b = root.GetEntry("b")!;
        root.RemoveEntry("b");
        root.InsertEntry(0, b);

        AssertEdited("b: |\n  x\na: 1", tree);
        Assert.AreEqual("x\n", ((YamlEditableScalar)b.Value).Value);
    }

    [TestMethod]
    public void ToSyntaxTree_ValueOfANewScalarChangedByALaterEdit_Throws()
    {
        var tree = YamlEditableTree.Parse("a: 1\nb: old\n");
        var root = RootOf(tree);
        root.SetValue("b", new YamlEditableScalar("|\n  x"));
        AssertEdited("a: 1\nb: |\n  x\n", tree);

        // Without the line break of its last line, the value of the block scalar would be "x", not "x\n".
        root.GetEntry("b")!.EndOfLineTrivia!.Clear();

        Assert.AreEqual("a: 1\nb: |\n  x", tree.Serialize());
        Assert.ThrowsExactly<InvalidOperationException>(() => tree.ToSyntaxTree());
    }

    [TestMethod]
    [DataRow("a: 1\n# about b\n\nb: 2\nc: 3\n", "# about b\n\nb: 2\nc: 3\n", "c: 3\n")]
    [DataRow("a: 1\n\n# Section B\n\nb: 2\nc: 3\n", "\n# Section B\n\nb: 2\nc: 3\n", "c: 3\n")]
    public void RemoveEntry_FirstKeyThenItsNext_RemovesTheCommentsOfTheNext(string yaml, string withoutA, string withoutB)
    {
        var tree = YamlEditableTree.Parse(yaml);

        RootOf(tree).RemoveEntry("a");
        AssertEdited(withoutA, tree);

        RootOf(tree).RemoveEntry("b");
        AssertEdited(withoutB, tree);
    }

    [TestMethod]
    public void ToSyntaxTree_MissingValueReplacedByAnEmptyStringThroughTrivia_Throws()
    {
        var tree = YamlEditableTree.Parse("a:\n");

        RootOf(tree).Entries[0].SeparatorTrivia = [new YamlEditableTrivia(YamlEditableTriviaKind.Whitespace, " "), new YamlEditableTrivia(YamlEditableTriviaKind.Comment, "#x")];
        Assert.AreEqual("a: #x\n", tree.Serialize());
        tree.ToSyntaxTree();

        Assert.ThrowsExactly<ArgumentException>(() => new YamlEditableTrivia(YamlEditableTriviaKind.Whitespace, " \"\""));
        Assert.ThrowsExactly<ArgumentException>(() => new YamlEditableTrivia(YamlEditableTriviaKind.Comment, "x"));
        Assert.ThrowsExactly<ArgumentException>(() => new YamlEditableTrivia(YamlEditableTriviaKind.Comment, "# a\nb: 1"));
        Assert.ThrowsExactly<ArgumentException>(() => new YamlEditableTrivia(YamlEditableTriviaKind.NewLine, "\n\n"));
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => new YamlEditableTrivia((YamlEditableTriviaKind)42, ""));
    }

    [TestMethod]
    [DataRow("a: old\n", "---", "a: ---\n", "---")]
    [DataRow("a: old", "|\n  x", "a: |\n  x\n", "x\n")]
    [DataRow("a: old\n", "'it''s'", "a: 'it''s'\n", "it's")]
    public void Value_ScalarSetInATree_IsTheStringTheTreeReadsItAs(string yaml, string text, string expected, string value)
    {
        var tree = YamlEditableTree.Parse(yaml);
        var scalar = new YamlEditableScalar(text);

        RootOf(tree).SetValue("a", scalar);

        Assert.AreEqual(value, scalar.Value);
        AssertEdited(expected, tree);
        Assert.AreEqual(value, scalar.Value);
    }

    [TestMethod]
    public void SetValue_QuotedValueBeforeAComment_KeepsTheComment()
    {
        var tree = YamlEditableTree.Parse("color: red # keep\n");

        RootOf(tree).SetValue("color", new YamlEditableScalar("'fff'"));

        AssertEdited("color: 'fff' # keep\n", tree);
    }

    [TestMethod]
    public void AddEntry_ListItemWhoseMapHasCommentLinesAboveItsFirstKey_StartsTheMapOnTheNextLine()
    {
        var tree = YamlEditableTree.Parse("group:\n  # note\n  a: 1\nitems:\n  - old\n");
        var root = RootOf(tree);
        var group = root.GetValue("group")!;
        root.SetValue("group", new YamlEditableMapping());

        ((YamlEditableSequence)root.GetValue("items")!).AddEntry(new YamlEditableSequenceEntry(group));

        AssertEdited("group: {}\nitems:\n  - old\n  -\n  # note\n    a: 1\n", tree);
        var reopened = RootOf(YamlEditableTree.Parse(tree.Serialize()));
        var item = (YamlEditableMapping)((YamlEditableSequence)reopened.GetValue("items")!).Items[1].Value;
        Assert.AreEqual("1", ((YamlEditableScalar)item.GetValue("a")!).Value);
    }

    [TestMethod]
    public void SetValue_BlockScalarWithATabInItsContent_IsIndentedUnderTheKey()
    {
        var tree = YamlEditableTree.Parse("a: old\n");

        RootOf(tree).SetValue("a", new YamlEditableScalar("|-\n\tdata"));

        AssertEdited("a: |-\n  \tdata\n", tree);
        Assert.AreEqual("\tdata", ((YamlEditableScalar)RootOf(tree).GetValue("a")!).Value);
    }

    [TestMethod]
    [DataRow("-\n  a: 1\n")]
    [DataRow("- a: 1\n")]
    [DataRow("k:\n  a: 1\n")]
    [DataRow("- # c\n  a: 1\n")]
    public void RemoveThenInsert_OnlyKeyOfAMapping_GivesBackTheText(string yaml)
    {
        var tree = YamlEditableTree.Parse(yaml);
        var map = tree.Root is YamlEditableSequence sequence ? (YamlEditableMapping)sequence.Items[0].Value : (YamlEditableMapping)RootOf(tree).GetValue("k")!;
        var a = map.Entries[0];

        map.RemoveEntry("a");
        map.InsertEntry(0, a);

        AssertEdited(yaml, tree);
    }

    [TestMethod]
    public void RemoveThenAddEntry_FirstKeyOfADocumentWithoutFinalLineBreak_KeepsNoFinalLineBreak()
    {
        var tree = YamlEditableTree.Parse("a: 1\nb: 2");
        var root = RootOf(tree);
        var a = root.Entries[0];

        root.RemoveEntry("a");
        root.AddEntry(a);

        AssertEdited("b: 2\na: 1", tree);
    }

    [TestMethod]
    public void SetValue_ScalarOnSeveralLinesInCrLfDocument_UsesCrLf()
    {
        var tree = YamlEditableTree.Parse("a: 1\r\n");

        RootOf(tree).SetValue("b", new YamlEditableScalar("|-\nx"));

        AssertEdited("a: 1\r\nb: |-\r\n  x\r\n", tree);
    }

    [TestMethod]
    public void Value_AnEntryAsTheValueOfAnEntry_ThrowsArgumentException()
    {
        var tree = YamlEditableTree.Parse("a: 1\nb: 2\n");
        var entry = new YamlEditableMappingEntry("c", new YamlEditableScalar("3"));

        Assert.ThrowsExactly<ArgumentException>(() => RootOf(tree).Entries[0].Value = entry);
        Assert.IsNull(entry.Parent);
    }

    [TestMethod]
    public void KeyTextAndKeyValue_QuotedKey_AreItsSourceAndItsString()
    {
        var tree = YamlEditableTree.Parse("\"a\\tb\": 1\n'c': 2\nd: 3\n");
        var entries = RootOf(tree).Entries;

        Assert.AreEqual("\"a\\tb\"", entries[0].KeyText);
        Assert.AreEqual("a\tb", entries[0].KeyValue);
        Assert.AreEqual("c", entries[1].KeyValue);
        Assert.AreSame(entries[2], RootOf(tree).GetEntry("d"));
        Assert.IsNull(RootOf(tree).GetEntry("e"));
    }
    [TestMethod]
    [DataRow("|-\n  x\n  \t", "a: |-\n  x\n  \t\n", "x\n\t")]
    [DataRow("|-\n  x\n     ", "a: |-\n  x\n     \n", "x\n   ")]
    [DataRow("|\n  x\n\n", "a: |\n  x\n", "x\n")]
    public void SetValue_BlockScalarWhoseLastLineIsSpaces_KeepsThatLine(string text, string expected, string value)
    {
        var tree = YamlEditableTree.Parse("a: old\n");
        Assert.AreEqual(value, new YamlEditableScalar(text).Value);

        RootOf(tree).SetValue("a", new YamlEditableScalar(text));

        AssertEdited(expected, tree);
        Assert.AreEqual(value, ((YamlEditableScalar)RootOf(YamlEditableTree.Parse(expected)).GetValue("a")!).Value);
    }

    [TestMethod]
    public void ToSyntaxTree_HeaderOfTheDocumentReadAsTheCommentOfItsFirstEntry_Throws()
    {
        var tree = YamlEditableTree.Parse("# doc\n\na: 1\nb: 2\n");
        var leading = tree.Root.LeadingTrivia!;

        leading.RemoveAt(leading.Count - 1);

        Assert.AreEqual("# doc\na: 1\nb: 2\n", tree.Serialize());
        Assert.ThrowsExactly<InvalidOperationException>(() => tree.ToSyntaxTree());
    }

    [TestMethod]
    [DataRow("-\n  a: 1")]
    [DataRow("- a: 1\r\n  b: 2\n")]
    [DataRow("k:\n  a: 1")]
    public void RemoveThenInsert_FirstKey_GivesBackTheTextWithItsLineBreaks(string yaml)
    {
        var tree = YamlEditableTree.Parse(yaml);
        var map = tree.Root is YamlEditableSequence sequence ? (YamlEditableMapping)sequence.Items[0].Value : (YamlEditableMapping)RootOf(tree).GetValue("k")!;
        var a = map.Entries[0];

        map.RemoveEntry("a");
        map.InsertEntry(0, a);

        AssertEdited(yaml, tree);
    }

    [TestMethod]
    public void AddEntry_MovedToAnEmptyMappingAtTheEndOfADocumentWithoutFinalLineBreak_KeepsNoFinalLineBreak()
    {
        var tree = YamlEditableTree.Parse("a: 1\nb: {}");
        var root = RootOf(tree);
        var a = root.GetEntry("a")!;

        root.RemoveEntry("a");
        ((YamlEditableMapping)root.GetValue("b")!).AddEntry(a);

        AssertEdited("b:\n  a: 1", tree);
    }
    [TestMethod]
    public void SetValue_NewKeyWithAnIndentationAndABlockScalar_KeepsTheIndentationOfItsLines()
    {
        var tree = YamlEditableTree.Parse("a: 1\n");

        RootOf(tree).SetValue("b", new YamlEditableScalar("|-\n  x\n    y"), "    ");

        AssertEdited("a: 1\nb: |-\n  x\n    y\n", tree);
        Assert.AreEqual("x\n  y", ((YamlEditableScalar)RootOf(tree).GetValue("b")!).Value);
    }

    [TestMethod]
    public void AddEntry_SequenceEntryWithAnIndentationAndABlockScalar_KeepsTheIndentationOfItsLines()
    {
        var tree = YamlEditableTree.Parse("- 1\n");
        var scalar = new YamlEditableScalar("|-\n  x\n    y");

        ((YamlEditableSequence)tree.Root).AddEntry(new YamlEditableSequenceEntry(scalar, "    "));

        AssertEdited("- 1\n- |-\n  x\n    y\n", tree);
        Assert.AreEqual("x\n  y", scalar.Value);
    }

    [TestMethod]
    public void SetValue_BlockScalarOnALineWithTrailingSpaces_PutsTheSpacesOnItsHeaderLine()
    {
        var tree = YamlEditableTree.Parse("a: old  \nb: 2\n");

        RootOf(tree).SetValue("a", new YamlEditableScalar("|-\n  x"));

        AssertEdited("a: |-  \n  x\nb: 2\n", tree);
        Assert.AreEqual("x", ((YamlEditableScalar)RootOf(tree).GetValue("a")!).Value);
    }

    [TestMethod]
    public void RemoveThenInsert_LastKeyOfDocumentWithMixedLineBreaks_GivesBackTheText()
    {
        var tree = YamlEditableTree.Parse("a: 1\r\nb: 2\nc: 3");
        var c = RootOf(tree).GetEntry("c")!;

        RootOf(tree).RemoveEntry("c");
        RootOf(tree).InsertEntry(2, c);

        AssertEdited("a: 1\r\nb: 2\nc: 3", tree);
    }

    [TestMethod]
    public void AddEntry_EntryOfAnotherTreeAddedToARootEmptyMapping_EndsWithOneLineBreak()
    {
        var source = YamlEditableTree.Parse("a: 1\n");
        var target = YamlEditableTree.Parse("{}\n");
        var a = RootOf(source).GetEntry("a")!;

        RootOf(source).RemoveEntry("a");
        RootOf(target).AddEntry(a);

        AssertEdited("a: 1\n", target);
    }

    [TestMethod]
    public void RemoveThenSetValue_OnlyKeyOfIndentedRoot_KeepsTheIndentationAndTheLineBreak()
    {
        var tree = YamlEditableTree.Parse("  a: 1\n");

        RootOf(tree).RemoveEntry("a");
        RootOf(tree).SetValue("b", new YamlEditableScalar("2"));

        AssertEdited("  b: 2\n", tree);
    }
    [TestMethod]
    [DataRow("{}  \n", "1", "a: 1  \n")]
    [DataRow("{}  \n", "|-\n  x", "a: |-  \n  x\n")]
    [DataRow("{}  ", "1", "a: 1  ")]
    public void SetValue_RootEmptyMappingWithSpacesAfterIt_KeepsTheSpacesOnTheLineOfTheEntry(string yaml, string text, string expected)
    {
        var tree = YamlEditableTree.Parse(yaml);

        RootOf(tree).SetValue("a", new YamlEditableScalar(text));

        AssertEdited(expected, tree);
    }

    [TestMethod]
    public void AddEntry_EntryOfAnotherTreeAddedToARootEmptyMappingWithSpaces_KeepsOneLineBreak()
    {
        var source = YamlEditableTree.Parse("a: 1\n");
        var target = YamlEditableTree.Parse("{}  \n");
        var a = RootOf(source).GetEntry("a")!;

        RootOf(source).RemoveEntry("a");
        RootOf(target).AddEntry(a);

        AssertEdited("a: 1  \n", target);
    }

    [TestMethod]
    public void Text_RootScalarWithSpacesAfterItBecomesBlockScalar_PutsTheSpacesOnItsHeaderLine()
    {
        var tree = YamlEditableTree.Parse("old  \n");

        ((YamlEditableScalar)tree.Root).Text = "|-\n  x";

        AssertEdited("|-  \n  x\n", tree);
    }

    [TestMethod]
    public void RemoveThenInsertFirst_EntryThatWasTheLastLine_GetsBackItsLineBreak()
    {
        var tree = YamlEditableTree.Parse("a: 1\r\nb: 2\nc: 3");
        var root = RootOf(tree);
        var b = root.GetEntry("b")!;

        root.RemoveEntry("c");
        root.RemoveEntry("b");
        root.InsertEntry(0, b);

        AssertEdited("b: 2\na: 1", tree);
    }
}
