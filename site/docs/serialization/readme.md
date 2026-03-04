---
title: Serialization
---

[`YamlSerializer`](xref:SharpYaml.YamlSerializer) maps YAML documents to .NET objects. It is designed to feel familiar if you already use [`JsonSerializer`](xref:System.Text.Json.JsonSerializer):

- [`YamlSerializer`](xref:SharpYaml.YamlSerializer) mirrors [`JsonSerializer`](xref:System.Text.Json.JsonSerializer)
- [`YamlSerializerOptions`](xref:SharpYaml.YamlSerializerOptions) mirrors [`JsonSerializerOptions`](xref:System.Text.Json.JsonSerializerOptions)
- source-generation uses [`YamlSerializerContext`](xref:SharpYaml.Serialization.YamlSerializerContext) and [`YamlTypeInfo<T>`](xref:SharpYaml.YamlTypeInfo`1)
- many common `System.Text.Json.Serialization` attributes work out of the box (for example [`JsonPropertyNameAttribute`](xref:System.Text.Json.Serialization.JsonPropertyNameAttribute), [`JsonIgnoreAttribute`](xref:System.Text.Json.Serialization.JsonIgnoreAttribute))

Start here:

- [YamlSerializer and options](overview.md)
- [JSON attribute interop](json-attributes.md)
