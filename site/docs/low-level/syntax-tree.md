---
title: Syntax tree and spans
---

The syntax layer is designed for scenarios where you need:

- Lossless parsing and re-emitting (including trivia such as whitespace and comments)
- Precise source locations (file name, line, column, span) for diagnostics and tooling

`YamlSyntaxTree` is an immutable source snapshot with a **flat token list**, not a mutable
mapping/sequence object model. `ToFullString()` and `WriteTo()` return the snapshot's text
without formatting it. To edit a file while preserving the surrounding source, use
`WithTextChange` as shown below. There is no structural `ReplaceNode` API or automatic
comment attachment/reformatting when moving nodes.

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

## Which API should I use?

| Goal | API | Source preservation |
| --- | --- | --- |
| Edit known source ranges, retaining surrounding comments/formatting | `YamlSyntaxTree.WithTextChange` | All text outside edited ranges is unchanged. |
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
