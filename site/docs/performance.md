---
title: Performance
---

SharpYaml is built for configuration workloads: fast parsing/emitting and low allocation object mapping.

## Key ideas

- Prefer [`YamlTypeInfo<T>`](xref:SharpYaml.YamlTypeInfo`1) for AOT and performance-critical paths.
- Prefer generated [`YamlTypeInfo<T>`](xref:SharpYaml.YamlTypeInfo`1) properties (for example `MyContext.Default.MyConfig`) over resolving by `Type` in tight loops.
- When using runtime converters with a generated context, resolve the generated `YamlTypeInfo<T>` once for that options instance and reuse it so converter replacement is initialized once.
- Reuse [`YamlSerializerOptions`](xref:SharpYaml.YamlSerializerOptions) instances (they are immutable and safe to cache). The converters and the reflection metadata resolved for an options instance are kept with it, so a new options instance for each call resolves them again each time.
- For allocation-sensitive output, use the `Stream`, `TextWriter` or `IBufferWriter<char>` overloads: they write through a pooled buffer and allocate no `string` for the document.

## How input is read

The parser reads a `string` in place, which is its fastest path. To use that path for files as well, a `Stream` or a `StreamReader` over a **seekable** stream (a `FileStream`, a `MemoryStream`) of up to 1 MB is read to its end at once, into one string, before parsing starts.

Any other input is read in blocks into a small pooled window that the parser scans in place: a larger seekable stream, so that parsing it does not need a string of twice its size, a stream that cannot seek (a pipe, a network stream), and any other `TextReader`. This path takes about 1.5 times as long as a string and allocates nothing for the text of the document.

The window only asks for more input when the parser needs a character it does not have. A document is therefore parsed as soon as it arrived, while the writer of the stream is still open. A `StreamReader` is asked for a block and returns what it has; any other `TextReader`, which may block until it read all it was asked for, is only asked for the characters needed.

## What the serializer reuses

- Reading a document reuses the tokens and events of the parser, so they are not allocated once per scalar. This only applies to the serializer: `Parser.CreateParser` and `Scanner<T>` still return a new object per event and token, which you can keep.
- The strings of short plain mapping keys are shared within a document: the keys that repeat in a list of mappings are one `string` instance.

## Benchmarks

See [Benchmarks](../benchmarks.md) for how to run the benchmark suite and how to interpret the results.
