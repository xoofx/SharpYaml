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

Any other input is read into a small pooled window that the parser scans in place, which allocates nothing for the text of the document:

- A larger seekable stream is read in blocks, so that parsing it does not need a string of twice its size.
- A `Stream` that cannot seek (a pipe, a network stream) is read as its bytes arrive: the serializer decodes what each read of the stream returns and never asks it to fill a buffer.
- Any other `TextReader`, a `StreamReader` over such a stream included, is only asked for the characters the parser needs. Such a reader may wait for its writer: asked for more than the document holds, it would wait for characters that are not coming.

In each case a document is parsed as soon as it arrived, while the writer of the stream is still open. The first two take about 1.5 times as long as a string, the last one about 3.5 times: pass the `Stream` itself rather than a `StreamReader` over it when it cannot seek.

## What the serializer reuses

- Reading a document reuses the tokens and events of the parser, so they are not allocated once per scalar. This only applies to the serializer: `Parser.CreateParser` and `Scanner<T>` still return a new object per event and token, which you can keep.
- The strings of short plain mapping keys are shared within a document: the keys that repeat in a list of mappings are one `string` instance.
- A polymorphic value is read twice, once to find its discriminator and once as the type it names. The second time reads the events kept from the first, so the value is neither written back to text nor parsed again, and an error in it is reported at its position in the document.

## Benchmarks

See [Benchmarks](../benchmarks.md) for how to run the benchmark suite and how to interpret the results.
