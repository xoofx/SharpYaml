---
title: Documents and streams
---

YAML supports multiple documents in a single stream.

When you need:

- a lossless representation with trivia and spans, use the syntax APIs (for example [`YamlSyntaxTree`](xref:SharpYaml.Syntax.YamlSyntaxTree))
- a structured node representation for dynamic manipulation, use the model APIs (for example [`YamlStream`](xref:SharpYaml.Model.YamlStream))

## Load a multi-document stream

```csharp
using System.IO;
using SharpYaml.Model;

var yaml = """
---
a: 1
---
b: 2
""";

var stream = YamlStream.Load(new StringReader(yaml));

var first = stream[0];
var second = stream[1];
```

## Serialize back to text

Both [`YamlDocument`](xref:SharpYaml.Model.YamlDocument) and [`YamlStream`](xref:SharpYaml.Model.YamlStream) can be written back:

```csharp
var text = stream.ToString();
```
