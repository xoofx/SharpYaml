---
uid: SharpYaml.Events
---

# Summary
Event model produced by the YAML parser and consumed by the emitter.

# Remarks
SharpYaml's low-level parser produces a stream of parsing events (for example stream/document start and end, mappings, sequences, and scalars).
These types are useful when you want to process YAML without building a DOM or mapping to .NET objects.

