---
title: YAML model
---

SharpYaml includes a YAML object model that can be used to inspect or author YAML dynamically.

The model is distinct from:

- the syntax tree (lossless text + spans)
- the object serializer (mapping to .NET types)

The mutable model does not retain comments or exact source formatting. To change a source
range while preserving the rest of a file, see [trivia-preserving syntax edits](../low-level/syntax-tree.md#edit-a-value-without-losing-comments).

