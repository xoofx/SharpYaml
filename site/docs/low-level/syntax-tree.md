---
title: Syntax tree and spans
---

The syntax layer is designed for scenarios where you need:

- Lossless parsing and re-emitting (including trivia such as whitespace and comments)
- Precise source locations (file name, line, column, span) for diagnostics and tooling

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
