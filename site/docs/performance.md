---
title: Performance
---

SharpYaml is built for configuration workloads: fast parsing/emitting and low allocation object mapping.

## Key ideas

- Prefer `YamlTypeInfo<T>` for AOT and performance-critical paths.
- Reuse `YamlSerializerOptions` instances (they are immutable and safe to cache).
- For large YAML payloads, consider `TextReader`/`TextWriter` overloads to avoid extra copies.

## Benchmarks

See [Benchmarks](../benchmarks/readme.md) for how to run the benchmark suite and how to interpret the results.

