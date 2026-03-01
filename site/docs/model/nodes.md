---
title: Nodes and values
---

The model layer represents YAML content as nodes:

- `YamlValue` for scalar values
- `YamlMapping` for mappings (key/value pairs)
- `YamlSequence` for sequences (lists)

This is useful for:

- reading YAML without binding to a fixed CLR type
- building YAML programmatically
- representing extension data (`YamlExtensionData`) as a mapping

