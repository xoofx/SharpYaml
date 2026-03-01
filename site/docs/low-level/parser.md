---
title: Scanner and parser
---

SharpYaml provides a scanner and parser for stream-oriented YAML processing.

Typical uses:

- Build tooling (formatters, linters, analyzers)
- Implement custom transformations on a YAML stream
- Build your own emitter or serializer layer

The public surface includes token/event abstractions and location tracking.

If you are starting from scratch, prefer the syntax tree APIs when you need lossless roundtrip and spans.

## Parsing events

The `Parser` produces a stream of parsing events:

```csharp
using System.IO;
using SharpYaml;

var yaml = "a: 1\n";
var parser = Parser.CreateParser(new StringReader(yaml));

while (parser.MoveNext())
{
    var evt = parser.Current;
    // Inspect evt (StreamStart, DocumentStart, MappingStart, Scalar, ...)
}
```

For higher-level consumption, you can use the model layer (`YamlStream.Load(...)`), which is built on top of the parser.

## Scanning tokens

The scanner is lower level than the parser. It tokenizes YAML input before parsing.

```csharp
using SharpYaml;

var scanner = new Scanner<StringLookAheadBuffer>(new StringLookAheadBuffer("a: 1\n"));
while (scanner.MoveNext())
{
    var token = scanner.Current;
    // Inspect token type, spans, and scalar values.
}
```
