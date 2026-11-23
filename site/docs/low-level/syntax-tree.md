---
title: Syntax tree and spans
---

The syntax layer is designed for scenarios where you need:

- Lossless parsing and re-emitting (including trivia such as whitespace and comments)
- Precise source locations (file name, line, column, span) for diagnostics and tooling

`YamlSyntaxTree` is an immutable source snapshot with a **flat token list**, not a mutable
mapping/sequence object model. `ToFullString()` and `WriteTo()` return the snapshot's text
without formatting it. To edit a file while preserving the surrounding source, use
`WithTextChange` as shown below. To edit by key instead of by offset, use the
[editable tree](#edit-by-key-with-the-editable-tree), a bounded layer on top of `WithTextChange`.

## Parse a document

```csharp
using SharpYaml.Syntax;

var text = "a: 1\n";
var tree = YamlSyntaxTree.Parse(text);
```

To include or exclude trivia tokens (whitespace/newlines/comments), use [`YamlSyntaxOptions`](xref:SharpYaml.Syntax.YamlSyntaxOptions):

```csharp
var tree = YamlSyntaxTree.Parse(text, new YamlSyntaxOptions { IncludeTrivia = false });
```

`IncludeTrivia = false` excludes trivia from the token list; it does not remove comments or
whitespace from the stored source or its output.

## Edit a value without losing comments

[`YamlSyntaxTree.WithTextChange`](xref:SharpYaml.Syntax.YamlSyntaxTree.WithTextChange(System.Int32,System.Int32,System.String))
replaces a character range and returns a new, validated tree. Every character outside that
range is retained, including comments, whitespace, line endings, anchors, aliases, and merge keys.

```csharp
using System.Linq;
using SharpYaml.Syntax;

var text = "# deployment\r\nimage: myapp:v1 # keep this comment\r\n";
var tree = YamlSyntaxTree.Parse(text);

// This example has a unique scalar spelling. For more complex documents, use
// parser events/spans to identify the desired value rather than matching text alone.
var value = tree.Tokens.Single(token =>
    token.Kind == YamlSyntaxKind.Scalar && token.Text == "myapp:v1");

var edited = tree.WithTextChange(
    value.Span.Start.Index,
    value.Span.End.Index - value.Span.Start.Index,
    "myapp:v2");

var output = edited.ToFullString();
// # deployment\r\nimage: myapp:v2 # keep this comment\r\n
// The original tree still contains myapp:v1.
```

The replacement is **raw YAML source**, not an automatically escaped CLR value. For example,
use `"\"null\""` as replacement text to write the quoted YAML string `"null"`, rather than
the YAML null scalar. Supply the appropriate indentation and line endings for multiline edits.
Use a zero-length range to insert source or an empty replacement to delete source.

The entire result is reparsed, so invalid YAML throws `YamlException` and the original tree
remains unchanged. Tokens and spans in the returned tree reflect the new text. For subsequent
edits, obtain offsets from that tree rather than reusing stale offsets. This is source-range
editing, not incremental parsing or a semantic object editor.

## Edit by key with the editable tree

[`YamlEditableTree`](xref:SharpYaml.Syntax.Editable.YamlEditableTree) groups the tokens of a
document into maps, lists and scalars, with comments and blank lines attached to the nodes.
You find a value by key, change it, add or remove entries, and then call
[`ToSyntaxTree`](xref:SharpYaml.Syntax.Editable.YamlEditableTree.ToSyntaxTree). It applies all
the edits to the source snapshot as one `WithTextChange` over the smallest range that differs,
so the result is validated and every character outside that range is retained.

```csharp
using SharpYaml.Syntax.Editable;

var text = "# servers\nServers:\n  - Name: prod # main\n    Port: 1521\n";
var tree = YamlEditableTree.Parse(text);

var servers = (YamlEditableList)((YamlEditableMap)tree.Root).GetValue("Servers")!;
var prod = (YamlEditableMap)servers.Items[0].Value;
var name = ((YamlEditableScalar)prod.GetValue("Name")!).Value; // "prod"
prod.SetValue("Port", new YamlEditableScalar("1522"));
prod.SetValue("Host", new YamlEditableScalar("db1"));

var edited = tree.ToSyntaxTree();
// # servers\nServers:\n  - Name: prod # main\n    Port: 1522\n    Host: db1\n
```

### Comments and layout

- Each comment belongs to an entry, or to the document: a comment on the line of an entry is
  the entry's, and comment lines are the entry's below them. Comment lines at the start of the
  document that a blank line separates from the first entry are the document's header, and
  comment lines after the last entry are the document's.
- Removing an entry removes its lines and its comments, and nothing else. Edits keep every other
  comment with its owner: the comment on the line of `key: {} # comment` or `- value # comment`
  stays on the line of the key or the `-` when the value becomes a block, and a blank line
  separates the comments of a document without content from the first entry added to it.
- `AddEntry`, `InsertEntry` and `SetValue` write a new entry on its own line, at the column of
  the other entries, with the line break of the document. An entry moved from another map or list
  takes that column too, with the lines of its value. A new map, list or scalar on several lines
  whose lines are not indented under their key or `-` is indented when it is set as a value.
- Undoing an edit gives back the text: adding an entry to an empty map or list and removing it, or
  removing the first entry of a list item and inserting an entry first again. A document read
  without a final line break keeps ending without one.
- A comment is never written after the last line of a block scalar, where it would be part of its
  value: it goes on the header line of the scalar (`key: | # comment`).

### Checks

`ToSyntaxTree` reads the edited text back and throws `InvalidOperationException`, without
changing `SyntaxTree`, unless it reads as the edited tree: the same maps, lists and scalars, each
scalar and key read exactly from the text written for it, each scalar whose text was not set read as
the value it had (wherever it was moved), and each comment read as the comment of the same entry.
It throws `YamlException` when the edited text is not valid YAML. These checks cover the edits
of the trivia lists (`LeadingTrivia`, `TrailingTrivia`, `EndOfLineTrivia`) as well. `Serialize`,
`WriteTo` and `ToString` write the same text without these checks.

An entry cannot be added after a block scalar (`|` or `>`, but `|-` is fine) that ends the document
without a line break (`InvalidOperationException`): the line break would become part of the
scalar's value.

### Scalars and keys

- `YamlEditableScalar.Text` is **raw YAML source**, as with `WithTextChange`, with its quotes:
  use `"\"null\""` to write the string `null`. `YamlEditableScalar.Value` is the string the
  text is read as, without quotes and with escapes resolved; it is not the value of a schema (the
  text `1` is the string `1`). A key or a list item without a value (`key:`, `-`) is a scalar
  with empty text.
- A key is found by its value, so `GetValue("name")` also finds `"name": x`; keys are compared
  by this text value, not by the value a schema resolves (`1` and `0x1` are two keys).
- `SetValue` writes a new key as a plain scalar and throws `ArgumentException` for a key that
  needs quotes: add a `YamlEditableMapEntry` with the quoted text of the key instead. `AddEntry`
  and `InsertEntry` reject a key that the mapping already has.

### Scope

The editable tree supports block-style mappings and sequences (also a sequence at the column of
its key, `key:\n- a`), plain, quoted and block scalars (`|`, `>`, also with `-`), `#` comments,
and empty mappings and sequences. An empty mapping or sequence has no block form: it is read
from and written as `{}` or `[]` (also with spaces inside), so removing the last entry of a map
or a list writes `{}` or `[]`, also at the root, and adding an entry to it writes it as a block again.

`YamlEditableTree.Parse` throws `NotSupportedException` for flow style with content or on several
lines, explicit keys (`?`), spaces between a key and its `:` (`a : b`), block scalars with the keep
indicator or an indentation indicator (`|+`, `|2`, whose value depends on the lines around them),
anchors, aliases, tags, directives, document markers (`---`, `...`, so also for a stream of several
documents), a mapping with the same key twice, and any document whose text the tree would not write
back exactly (a byte order mark is kept), so a document that uses them is never edited partially.

## Which API should I use?

| Goal | API | Source preservation |
| --- | --- | --- |
| Edit known source ranges, retaining surrounding comments/formatting | `YamlSyntaxTree.WithTextChange` | All text outside edited ranges is unchanged. |
| Change, add or remove entries by key, retaining comments/formatting (block style only) | `YamlEditableTree` | Unedited nodes keep their original text; the result is applied through `WithTextChange`. |
| Add/remove mappings or sequences through a mutable object model | `YamlMapping`, `YamlSequence`, `YamlDocument` | Not lossless; comments/formatting are not retained and aliases are materialized as copies. |
| Transform parsing events and emit YAML | `Parser`, `Emitter` | Can retain anchor/alias/merge events, but not original formatting/comments. |
| Read/write .NET configuration objects | `YamlSerializer` | Data mapping, not source editing; merge keys are expanded. |

## Spans and marks

SharpYaml uses source locations for errors and editor integrations.

- [`Mark`](xref:SharpYaml.Mark) captures index, line, and column.
- [`YamlSourceSpan`](xref:SharpYaml.Syntax.YamlSourceSpan) captures a start/end mark.

Parse errors throw [`YamlException`](xref:SharpYaml.YamlException) with location information.

## Tokens

The syntax tree exposes a flat list of [`YamlSyntaxToken`](xref:SharpYaml.Syntax.YamlSyntaxToken) with spans and text.

This is useful for:

- syntax highlighting
- quick diagnostics
- building editor features without building a full semantic model