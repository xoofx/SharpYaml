using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SharpYaml.Syntax.Editable;

namespace SharpYaml.Tests.Syntax.Editable;

/// <summary>
/// Checks properties of the editable tree that must hold for every document and every edit, over a corpus of documents
/// and seeded random sequences of edits: an edit is either applied or rejected with <see cref="InvalidOperationException"/>,
/// an applied edit keeps every comment with the entry that owns it (also when the text is read again), and adding then
/// removing an entry gives back the original text.
/// </summary>
/// <remarks>
/// Each comment of the corpus has a unique id ("#c1"), so that the test can tell which entry owns it.
/// </remarks>
[TestClass]
public class YamlEditableTreePropertyTests
{
    private static readonly string[] Corpus =
    [
        "#c1 header\n\n#c2 about name\nname: app #c3\nport: 80\n",
        "#c1\nServers:\n  #c2\n  - Name: prod #c3\n    Port: 1521\n  #c4\n  - Name: dev\n    Port: 1522\nOther: x #c5\n",
        "a: {} #c1\nb: [] #c2\nc:\n  {} #c3\nd: 1\n",
        "- {} #c1\n- [] #c2\n-\n  #c3\n  {}\n- tail #c4\n",
        "{} #c1\n",
        "[]\n",
        "#c1 only comments\n",
        "",
        "text: | #c1\n  line 1\n  line 2\nnext: 1 #c2\nfold: >-\n  folded\n  text\nlast: x\n",
        "a: 1\r\nb:\r\n  - 2 #c1\r\n  - 3\r\n",
        "key:\n  value #c1\nlast: 1",
        "k:\n  - - a #c1\n    - b\n  - c\n",
        "-\n  a: 1\n  b: 2\n- x: 1 #c1\n  #c2\n  y: 2\n",
        "\"quoted key\": 1\n'single': 2 #c1\nplain: \"multi\n  line\"\n",
        "outer:\n  inner:\n    deep: 1 #c1\n    #c2\n    deeper:\n      - 1\n      - 2\n  sibling: 2\n",
        "a:\nb: #c1\nc:\n  -\n",
        "list:\n- a #c1\n- b\nnext: 1\n",
        "  {} #c1\n",
        "a: x\n\n\n#c1\n\nb: y\n",
        "root: |-\n  strip\nother: >\n  fold\n\n#c1\nend: 1",
        "- a: 1 #c1\n  b: |\n    text\n  c: 3\n- - nested #c2\n  - more\n",
        "#c1\n\n- 1\n- 2 #c2\n",
        "value #c1\n",
        "a: 1 #c1",
        "a:\n  b: 1\n\n  #c1\n\n  c: 2\n",
        "- # c1 dash\n  a: 1\n  b: 2\n- #c2\n  - x\n",
        "a: #c1\n  b: 1 #c2\nc: #c3\n  - 1\n",
        "-     {} #c1\n- tail\n",
    ];

    private static readonly Regex CommentId = new(@"#c\d+|# c\d+", RegexOptions.Compiled);

    [TestMethod]
    public void Corpus_ParsesAndWritesBackExactly()
    {
        foreach (var yaml in Corpus)
        {
            var tree = YamlEditableTree.Parse(yaml);
            Assert.AreEqual(yaml, tree.Serialize(), yaml);
            Assert.AreSame(tree.SyntaxTree, tree.ToSyntaxTree(), yaml);
            Owners(tree);
        }
    }

    [TestMethod]
    public void AddThenRemove_AtEveryPositionOfEveryCollection_GivesBackTheText()
    {
        var failures = new List<string>();
        foreach (var yaml in Corpus)
        {
            var collectionCount = Collections(YamlEditableTree.Parse(yaml)).Count;
            for (var collectionIndex = 0; collectionIndex < collectionCount; collectionIndex++)
            {
                var count = EntryCount(Collections(YamlEditableTree.Parse(yaml))[collectionIndex]);
                for (var index = 0; index <= count; index++)
                {
                    for (var kind = 0; kind < ValueKinds; kind++)
                    {
                        var tree = YamlEditableTree.Parse(yaml);
                        var collection = Collections(tree)[collectionIndex];
                        var description = $"{Escape(yaml)} | collection {collectionIndex} | insert at {index} | value kind {kind}";
                        try
                        {
                            var before = Owners(tree);
                            Insert(collection, index, NewValue(kind, 0), "zz");
                            CheckCommit(tree, description, failures, before);
                            Remove(collection, index, "zz");
                            var text = tree.Serialize();
                            if (text != yaml)
                            {
                                failures.Add($"{description}: add then remove wrote {Escape(text)}");
                            }
                        }
                        catch (Exception exception) when (exception is not AssertFailedException)
                        {
                            failures.Add($"{description}: {exception.GetType().Name}: {exception.Message}");
                        }
                    }
                }
            }
        }

        AssertNoFailures(failures);
    }

    [TestMethod]
    public void RemoveThenInsertBack_EveryEntry_GivesBackTheText()
    {
        var failures = new List<string>();
        foreach (var yaml in Corpus)
        {
            var collectionCount = Collections(YamlEditableTree.Parse(yaml)).Count;
            for (var collectionIndex = 0; collectionIndex < collectionCount; collectionIndex++)
            {
                var count = EntryCount(Collections(YamlEditableTree.Parse(yaml))[collectionIndex]);
                for (var index = 0; index < count; index++)
                {
                    var tree = YamlEditableTree.Parse(yaml);
                    var collection = Collections(tree)[collectionIndex];
                    var description = $"{Escape(yaml)} | collection {collectionIndex} | remove and insert back {index}";
                    try
                    {
                        var entry = EntryAt(collection, index);
                        Remove(collection, index, null);
                        Reinsert(collection, index, entry);
                        var text = tree.Serialize();
                        if (text != yaml)
                        {
                            failures.Add($"{description}: wrote {Escape(text)}");
                        }
                    }
                    catch (Exception exception) when (exception is not AssertFailedException)
                    {
                        failures.Add($"{description}: {exception.GetType().Name}: {exception.Message}");
                    }
                }
            }
        }

        AssertNoFailures(failures);
    }

    [TestMethod]
    public void RandomEdits_AreAppliedOrRejected_AndKeepEachCommentWithItsOwner()
    {
        var failures = new List<string>();
        var rejected = 0;
        var applied = 0;
        // Set SHARPYAML_EDITABLE_SEEDS to run more sequences locally, e.g. 200000 after a change of the layout code.
        var seeds = int.TryParse(Environment.GetEnvironmentVariable("SHARPYAML_EDITABLE_SEEDS"), out var count) ? count : 3000;
        for (var seed = 0; seed < seeds; seed++)
        {
            var random = new Random(seed);
            var yaml = Corpus[random.Next(Corpus.Length)];
            var tree = YamlEditableTree.Parse(yaml);
            var original = Owners(tree);
            FindDetachable(tree);
            RequestedValues.Clear();
            var steps = new List<string>();
            var committed = Scalars(tree.Root).ToDictionary(scalar => scalar, scalar => scalar.Value, ReferenceEqualityComparer.Instance);
            var textSet = new HashSet<YamlEditableScalar>(ReferenceEqualityComparer.Instance);
            var operations = 1 + random.Next(4);
            for (var step = 0; step < operations; step++)
            {
                string operation;
                try
                {
                    operation = ApplyRandomEdit(tree, random, step, textSet);
                }
                catch (Exception exception) when (exception is not AssertFailedException)
                {
                    failures.Add($"seed {seed} {Escape(yaml)} | {string.Join(" ; ", steps)} | edit threw {exception.GetType().Name}: {exception.Message}");
                    break;
                }

                if (operation.Length == 0)
                {
                    continue;
                }

                steps.Add(operation);
                var description = $"seed {seed} {Escape(yaml)} | {string.Join(" ; ", steps)}";
                var outcome = CheckCommit(tree, description, failures, original);
                if (outcome == Outcome.Rejected)
                {
                    rejected++;
                    break;
                }

                if (outcome == Outcome.Failed)
                {
                    break;
                }

                // A new scalar is read as the value it was written for, and a scalar whose text was not set keeps the value it
                // had when the edits were last applied.
                foreach (var scalar in Scalars(tree.Root))
                {
                    var value = scalar.Value;
                    if (RequestedValues.TryGetValue(scalar, out var requested))
                    {
                        if (requested != value)
                        {
                            failures.Add($"{description}: {Escape(tree.Serialize())}: a new scalar written for {Escape(requested)} is read as {Escape(value)}");
                        }

                        RequestedValues.Remove(scalar);
                    }

                    if (!textSet.Contains(scalar) && committed.TryGetValue(scalar, out var before) && before != value)
                    {
                        failures.Add($"{description}: {Escape(tree.Serialize())}: a scalar read as {Escape(before)} is now read as {Escape(value)}");
                    }

                    committed[scalar] = value;
                }

                textSet.Clear();
                applied++;
            }
        }

        Console.WriteLine($"Applied {applied} edits, rejected {rejected}.");
        AssertNoFailures(failures);

        // The edits that the tree rejects are rare cases, such as an empty scalar at the root, which reads as no content.
        Assert.IsTrue(rejected * 100 < applied, $"{rejected} edits were rejected, for {applied} applied.");
    }

    // The entry whose comment lines a blank line separates from it, by comment: they become the header of the document when
    // the entry is the first entry of the root.
    [ThreadStatic]
    private static Dictionary<string, object>? _detachable;

    private static Dictionary<string, object> Detachable => _detachable ??= [];

    private static void FindDetachable(YamlEditableTree tree)
    {
        Detachable.Clear();
        var all = new List<YamlEditableNode>();
        void Collect(YamlEditableNode node)
        {
            foreach (var child in Entries(node))
            {
                all.Add(child);
                Collect(ValueOf(child));
            }
        }

        Collect(tree.Root);
        foreach (var entry in all)
        {
            var leading = entry.LeadingTrivia ?? [];
            var lastBlank = -1;
            for (var index = 0; index < leading.Count; index++)
            {
                if (leading[index].Kind == YamlEditableTriviaKind.NewLine && (index == 0 || leading[index - 1].Kind != YamlEditableTriviaKind.Comment))
                {
                    lastBlank = index;
                }
            }

            for (var index = 0; index < lastBlank; index++)
            {
                if (leading[index].Kind == YamlEditableTriviaKind.Comment)
                {
                    foreach (Match match in CommentId.Matches(leading[index].Text))
                    {
                        Detachable[match.Value] = entry;
                    }
                }
            }
        }
    }

    private enum Outcome
    {
        Applied,
        Rejected,
        Failed,
    }

    /// <summary>
    /// Commits the edits of <paramref name="tree"/> and checks them: the text reads back as the same tree, and each
    /// comment of <paramref name="original"/> whose owner is still in the tree is still its own, once.
    /// </summary>
    private static Outcome CheckCommit(YamlEditableTree tree, string description, List<string> failures, Dictionary<string, object> original)
    {
        string text;
        try
        {
            text = tree.ToSyntaxTree().ToFullString();
        }
        catch (InvalidOperationException)
        {
            return Outcome.Rejected;
        }
        catch (Exception exception)
        {
            failures.Add($"{description}: ToSyntaxTree threw {exception.GetType().Name}: {exception.Message} | wrote {Escape(tree.Serialize())}");
            return Outcome.Failed;
        }

        YamlEditableTree reopened;
        try
        {
            reopened = YamlEditableTree.Parse(text);
        }
        catch (Exception exception)
        {
            failures.Add($"{description}: reopening {Escape(text)} threw {exception.GetType().Name}: {exception.Message}");
            return Outcome.Failed;
        }

        var map = new Dictionary<object, object>(ReferenceEqualityComparer.Instance) { [tree] = reopened };
        if (!Pair(tree.Root, reopened.Root, map))
        {
            failures.Add($"{description}: {Escape(text)} reads back as another tree");
            return Outcome.Failed;
        }

        Dictionary<string, object> actual;
        try
        {
            actual = Owners(reopened);
        }
        catch (InvalidOperationException exception)
        {
            failures.Add($"{description}: {Escape(text)}: {exception.Message}");
            return Outcome.Failed;
        }

        var moved = new List<(string Id, object Owner)>();
        foreach (var (id, owner) in original)
        {
            var expected = IsAttached(owner, tree) ? map[owner] : null;
            actual.TryGetValue(id, out var actualOwner);

            // The comment lines that a blank line separates from the first entry of the document are read again as its
            // header: the text does not tell them apart.
            var isFirst = owner is YamlEditableNode node && Entries(tree.Root).FirstOrDefault() is { } first && ReferenceEquals(first, node);
            if (isFirst && Detachable.ContainsKey(id) && ReferenceEquals(actualOwner, reopened))
            {
                continue;
            }

            if (!ReferenceEquals(expected, actualOwner))
            {
                failures.Add($"{description}: {Escape(text)}: comment {id} is owned by {Describe(actualOwner)} instead of {Describe(expected)}");
                return Outcome.Failed;
            }
        }

        foreach (var id in actual.Keys)
        {
            if (!original.ContainsKey(id))
            {
                failures.Add($"{description}: {Escape(text)}: unexpected comment {id}");
                return Outcome.Failed;
            }
        }

        foreach (var (id, owner) in moved)
        {
            original[id] = owner;
        }

        return Outcome.Applied;
    }

    private const int ValueKinds = 8;

    private static YamlEditableNode NewValue(int kind, int id) => kind switch
    {
        0 => Requested("v" + id, "v" + id),
        1 => Requested("\"q" + id + "\"", "q" + id),
        2 => Requested(string.Empty, string.Empty),
        3 => new YamlEditableMapping(),
        4 => new YamlEditableSequence(),
        5 => NewMap(id),
        6 => NewList(id),
        _ => Requested("|\n  b" + id + "\n", "b" + id + "\n"),
    };

    // The value that each new scalar is expected to be read as, until the edits are first applied.
    [ThreadStatic]
    private static Dictionary<YamlEditableScalar, string>? _requested;

    private static Dictionary<YamlEditableScalar, string> RequestedValues => _requested ??= new(ReferenceEqualityComparer.Instance);

    private static YamlEditableScalar Requested(string text, string value)
    {
        var scalar = new YamlEditableScalar(text);
        RequestedValues[scalar] = value;
        return scalar;
    }

    private static YamlEditableMapping NewMap(int id)
    {
        var map = new YamlEditableMapping();
        map.SetValue("m" + id, Requested("1", "1"));
        map.SetValue("n" + id, Requested("2", "2"));
        return map;
    }

    private static YamlEditableSequence NewList(int id)
    {
        var list = new YamlEditableSequence();
        list.AddEntry(new YamlEditableSequenceEntry(Requested("i" + id, "i" + id), string.Empty));
        list.AddEntry(new YamlEditableSequenceEntry(Requested("j" + id, "j" + id), string.Empty));
        return list;
    }

    private static string ApplyRandomEdit(YamlEditableTree tree, Random random, int step, HashSet<YamlEditableScalar> textSet)
    {
        var collections = Collections(tree);
        var scalars = Scalars(tree.Root);
        var choice = random.Next(10);
        if (choice == 9 && scalars.Count > 0)
        {
            var scalar = scalars[random.Next(scalars.Count)];
            var (text, value) = random.Next(7) switch
            {
                0 => ("t" + step, "t" + step),
                1 => ("'t" + step + "'", "t" + step),
                2 => ("t" + step + "\nu" + step, "t" + step + " u" + step),
                3 => ("\"t" + step + "\n u" + step + "\"", "t" + step + " u" + step),
                4 => ("|\n t" + step + "\n", "t" + step + "\n"),
                5 => (">-\n  t" + step + "\n\n  u" + step, "t" + step + "\nu" + step),
                _ => (string.Empty, string.Empty),
            };
            scalar.Text = text;
            RequestedValues[scalar] = value;
            textSet.Add(scalar);
            return $"text {Path(scalar)} = {Escape(text)}";
        }

        if (collections.Count == 0)
        {
            return string.Empty;
        }

        var collection = collections[random.Next(collections.Count)];
        var count = EntryCount(collection);
        var kind = random.Next(ValueKinds);
        switch (random.Next(6))
        {
            case 5 when count > 0:
            {
                // Moves an entry to another collection of the same kind.
                var from = random.Next(count);
                var entry = EntryAt(collection, from);
                var targets = collections.FindAll(other => other.Kind == collection.Kind && !ReferenceEquals(other, collection) && !IsInside(other, entry));
                if (targets.Count == 0)
                {
                    return string.Empty;
                }

                var target = targets[random.Next(targets.Count)];
                if (entry is YamlEditableMappingEntry keyed && ((YamlEditableMapping)target).Entries.Any(other => other.KeyText == keyed.KeyText))
                {
                    return string.Empty;
                }

                Remove(collection, from, null);
                var to = random.Next(EntryCount(target) + 1);
                Reinsert(target, to, entry);
                return $"move {Path(collection)}[{from}] to {Path(target)}[{to}]";
            }

            case 0 when count > 0:
            {
                var index = random.Next(count);
                Remove(collection, index, null);
                return $"remove {Path(collection)}[{index}]";
            }
            case 1 when count > 0:
            {
                var index = random.Next(count);
                var entry = EntryAt(collection, index);
                var value = NewValue(kind, step);
                switch (entry)
                {
                    case YamlEditableMappingEntry mapEntry:
                        mapEntry.Value = value;
                        break;
                    case YamlEditableSequenceEntry listEntry:
                        listEntry.Value = value;
                        break;
                }

                return $"value {Path(collection)}[{index}] = kind {kind}";
            }
            case 2 when count > 1:
            {
                // Moves an entry inside its collection.
                var from = random.Next(count);
                var entry = EntryAt(collection, from);
                Remove(collection, from, null);
                var to = random.Next(count);
                Reinsert(collection, to, entry);
                return $"move {Path(collection)}[{from}] to {to}";
            }
            default:
            {
                var index = random.Next(count + 1);
                Insert(collection, index, NewValue(kind, step), "k" + step);
                return $"insert {Path(collection)}[{index}] kind {kind}";
            }
        }
    }

    private static void Insert(YamlEditableNode collection, int index, YamlEditableNode value, string key)
    {
        switch (collection)
        {
            case YamlEditableMapping map:
                map.InsertEntry(index, new YamlEditableMappingEntry(key, value, string.Empty));
                break;
            case YamlEditableSequence list:
                list.InsertEntry(index, new YamlEditableSequenceEntry(value, string.Empty));
                break;
        }
    }

    private static void Reinsert(YamlEditableNode collection, int index, YamlEditableNode entry)
    {
        switch (collection)
        {
            case YamlEditableMapping map:
                map.InsertEntry(index, (YamlEditableMappingEntry)entry);
                break;
            case YamlEditableSequence list:
                list.InsertEntry(index, (YamlEditableSequenceEntry)entry);
                break;
        }
    }

    private static void Remove(YamlEditableNode collection, int index, string? key)
    {
        switch (collection)
        {
            case YamlEditableMapping map:
                Assert.IsTrue(map.RemoveEntry(key ?? ((YamlEditableMappingEntry)EntryAt(map, index)).KeyText.Trim('"', '\'')));
                break;
            case YamlEditableSequence list:
                list.RemoveAt(index);
                break;
        }
    }

    private static YamlEditableNode EntryAt(YamlEditableNode collection, int index) => collection switch
    {
        YamlEditableMapping map => map.Entries[index],
        YamlEditableSequence list => list.Items[index],
        _ => throw new InvalidOperationException(),
    };

    private static int EntryCount(YamlEditableNode collection) => collection switch
    {
        YamlEditableMapping map => map.Entries.Count,
        YamlEditableSequence list => list.Items.Count,
        _ => 0,
    };

    private static List<YamlEditableNode> Collections(YamlEditableTree tree)
    {
        var result = new List<YamlEditableNode>();
        void Visit(YamlEditableNode node)
        {
            if (node is YamlEditableMapping or YamlEditableSequence)
            {
                result.Add(node);
            }

            foreach (var entry in Entries(node))
            {
                Visit(ValueOf(entry));
            }
        }

        Visit(tree.Root);
        return result;
    }

    private static List<YamlEditableScalar> Scalars(YamlEditableNode node)
    {
        var result = new List<YamlEditableScalar>();
        if (node is YamlEditableScalar scalar)
        {
            result.Add(scalar);
        }

        foreach (var entry in Entries(node))
        {
            result.AddRange(Scalars(ValueOf(entry)));
        }

        return result;
    }

    private static IEnumerable<YamlEditableNode> Entries(YamlEditableNode node) => node switch
    {
        YamlEditableMapping map => map.Entries,
        YamlEditableSequence list => list.Items,
        _ => [],
    };

    private static YamlEditableNode ValueOf(YamlEditableNode entry) => entry switch
    {
        YamlEditableMappingEntry mapEntry => mapEntry.Value,
        YamlEditableSequenceEntry listEntry => listEntry.Value,
        _ => throw new InvalidOperationException(),
    };

    /// <summary>
    /// Pairs the nodes of two trees of the same shape.
    /// </summary>
    private static bool Pair(YamlEditableNode left, YamlEditableNode right, Dictionary<object, object> map)
    {
        if (left.Kind != right.Kind)
        {
            return false;
        }

        map[left] = right;
        var leftEntries = Entries(left).ToList();
        var rightEntries = Entries(right).ToList();
        if (leftEntries.Count != rightEntries.Count)
        {
            return false;
        }

        for (var index = 0; index < leftEntries.Count; index++)
        {
            map[leftEntries[index]] = rightEntries[index];
            if (leftEntries[index] is YamlEditableMappingEntry leftKey && ((YamlEditableMappingEntry)rightEntries[index]).KeyText != leftKey.KeyText)
            {
                return false;
            }

            if (!Pair(ValueOf(leftEntries[index]), ValueOf(rightEntries[index]), map))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// Returns the owner of each comment of <paramref name="tree"/>: the entry whose lines hold it, or the tree for the
    /// comments of the document.
    /// </summary>
    private static Dictionary<string, object> Owners(YamlEditableTree tree)
    {
        var owners = new Dictionary<string, object>();
        void Add(IEnumerable<YamlEditableTrivia>? trivia, object owner)
        {
            foreach (var item in trivia ?? [])
            {
                if (item.Kind == YamlEditableTriviaKind.Comment)
                {
                    AddText(item.Text, owner);
                }
            }
        }

        void AddText(string text, object owner)
        {
            foreach (Match match in CommentId.Matches(text))
            {
                if (!owners.TryAdd(match.Value, owner))
                {
                    throw new InvalidOperationException($"comment {match.Value} is written twice");
                }
            }
        }

        void AddNode(YamlEditableNode node, object owner)
        {
            Add(node.LeadingTrivia, owner);
            if (node is YamlEditableEntry entry)
            {
                Add(entry.SeparatorTrivia, owner);
                Add(entry.EndOfLineTrivia, owner);
            }
            if (node is YamlEditableScalar scalar)
            {
                AddText(scalar.Text, owner);
            }
        }

        void Visit(YamlEditableNode entry)
        {
            AddNode(entry, entry);
            var value = ValueOf(entry);
            AddNode(value, entry);
            foreach (var child in Entries(value))
            {
                Visit(child);
            }
        }

        Add(tree.TrailingTrivia, tree);
        AddNode(tree.Root, tree);
        foreach (var entry in Entries(tree.Root))
        {
            Visit(entry);
        }

        return owners;
    }

    private static bool IsInside(YamlEditableNode node, YamlEditableNode ancestor)
    {
        for (var current = node; current is not null; current = current.Parent)
        {
            if (ReferenceEquals(current, ancestor))
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsAttached(object owner, YamlEditableTree tree)
    {
        if (owner is YamlEditableTree)
        {
            return ReferenceEquals(owner, tree);
        }

        if (owner is not YamlEditableNode node)
        {
            return false;
        }

        while (node.Parent is not null)
        {
            node = node.Parent;
        }

        return ReferenceEquals(node, tree.Root);
    }

    private static string Path(YamlEditableNode node)
    {
        var parts = new List<string>();
        var current = node;
        while (current.Parent is { } entry)
        {
            parts.Add(entry switch
            {
                YamlEditableMappingEntry mapEntry => mapEntry.KeyText,
                YamlEditableSequenceEntry listEntry when listEntry.Parent is YamlEditableSequence list => "[" + list.Items.ToList().IndexOf(listEntry) + "]",
                _ => "?",
            });

            if (entry.Parent is null)
            {
                break;
            }

            current = entry.Parent;
        }

        parts.Reverse();
        return "/" + string.Join("/", parts);
    }

    private static string Describe(object? owner) => owner switch
    {
        null => "nothing (lost)",
        YamlEditableTree => "the document",
        YamlEditableMappingEntry entry => "key " + entry.KeyText,
        YamlEditableSequenceEntry entry => "item " + Escape(entry.ToString()),
        _ => owner.ToString() ?? "?",
    };

    private static string Escape(string text) => "\"" + text.Replace("\r", "\\r").Replace("\n", "\\n") + "\"";

    private static void AssertNoFailures(List<string> failures)
    {
        if (failures.Count == 0)
        {
            return;
        }

        var message = new StringBuilder();
        message.AppendLine($"{failures.Count} failures:");
        foreach (var failure in failures.Take(40))
        {
            message.AppendLine(failure);
        }

        Assert.Fail(message.ToString());
    }
}