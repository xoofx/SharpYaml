---
title: Performance
---

SharpYaml is built for configuration workloads: fast parsing/emitting and low allocation object mapping.

## Key ideas

- Prefer `YamlTypeInfo<T>` for AOT and performance-critical paths.
- Prefer generated `YamlTypeInfo<T>` properties (for example `MyContext.Default.MyConfig`) over resolving by `Type` in tight loops.
- Reuse `YamlSerializerOptions` instances (they are immutable and safe to cache).
- For large YAML payloads, consider `TextReader`/`TextWriter` overloads to avoid extra copies.
- For allocation-sensitive output, consider `IBufferWriter<char>` overloads to avoid allocating a `string`.

## Benchmarks

See [Benchmarks](../benchmarks.md) for how to run the benchmark suite and how to interpret the results.
