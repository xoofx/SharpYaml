---
title: Documentation
---

SharpYaml has two main layers:

- **Low-level YAML infrastructure** for parsing/emitting and tooling scenarios (lossless roundtrip, source spans, diagnostics).
- **Object mapping** via [`YamlSerializer`](xref:SharpYaml.YamlSerializer) for configuration-style serialization/deserialization (not roundtrip-preserving).

## Where to start

- New to SharpYaml: [Getting started](getting-started.md)
- Coming from [`JsonSerializer`](xref:System.Text.Json.JsonSerializer): start with [Serialization overview](serialization/overview.md) and [JSON attribute interop](serialization/json-attributes.md)
- Writing tooling (syntax highlighting, diagnostics, refactorings): start with [Syntax tree](low-level/syntax-tree.md)
