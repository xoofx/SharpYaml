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

## Spans and marks

SharpYaml uses source locations for errors and editor integrations.

- `Mark` captures index, line, and column.
- `YamlSourceSpan` captures a start/end mark.

Parse errors throw `YamlException` with location information.

