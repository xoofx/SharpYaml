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

