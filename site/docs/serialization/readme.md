---
title: Serialization
---

`YamlSerializer` maps YAML documents to .NET objects. It is designed to feel familiar if you already use `System.Text.Json`:

- `YamlSerializer` mirrors `JsonSerializer`
- `YamlSerializerOptions` mirrors `JsonSerializerOptions`
- source-generation uses `YamlSerializerContext` and `YamlTypeInfo<T>`
- many common `System.Text.Json.Serialization` attributes work out of the box

Start here:

- [YamlSerializer and options](overview.md)
- [JSON attribute interop](json-attributes.md)

