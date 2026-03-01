---
title: Emitter and writer
---

The emitter writes YAML text from parsing events or higher-level structures.

## Formatting controls

Emission is influenced by options such as:

- indentation (`WriteIndented`, `IndentSize`)
- mapping ordering (`MappingOrder`)
- scalar style preferences (`ScalarStylePreferences`)

For object mapping, these are configured through `YamlSerializerOptions`.

