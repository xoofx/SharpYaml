---
title: YAML 1.2 and schemas
---

SharpYaml targets YAML 1.2 behavior and exposes schema control via [`YamlSerializerOptions.Schema`](xref:SharpYaml.YamlSerializerOptions.Schema).

Schemas influence how plain scalars are resolved:

- `"true"` can be a boolean
- `"123"` can be an integer
- `"null"` can be null

In configuration scenarios, schema choices are often a tradeoff between convenience and strictness.

## Schema selection

```csharp
using SharpYaml;

var options = new YamlSerializerOptions
{
    Schema = YamlSchemaKind.Core,
};
```

## Duplicate keys

Mappings with duplicate keys can be handled as errors or last-one-wins (depending on options):

```csharp
var options = new YamlSerializerOptions
{
    DuplicateKeyHandling = YamlDuplicateKeyHandling.Error,
};
```

## Merge keys (`<<`)

SharpYaml supports the YAML merge key (`<<`) when deserializing into .NET objects and `Dictionary<string, TValue>`.
This behavior is enabled for [`YamlSchemaKind.Core`](xref:SharpYaml.YamlSchemaKind.Core) and [`YamlSchemaKind.Extended`](xref:SharpYaml.YamlSchemaKind.Extended) (it is ignored for [`YamlSchemaKind.Json`](xref:SharpYaml.YamlSchemaKind.Json) and [`YamlSchemaKind.Failsafe`](xref:SharpYaml.YamlSchemaKind.Failsafe) schemas).

## Notes

- The syntax layer is designed for tooling and lossless roundtrip.
- The object-mapping layer is optimized for configuration and does not preserve formatting/comments.
