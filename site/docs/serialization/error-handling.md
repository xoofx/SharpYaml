---
title: Errors and diagnostics
---

SharpYaml throws [`YamlException`](xref:SharpYaml.YamlException) for parsing and (most) serialization failures.

## Parsing context

When possible, exceptions include:

- source name (for example a file path) via [`YamlSerializerOptions.SourceName`](xref:SharpYaml.YamlSerializerOptions.SourceName)
- line and column
- a span (start/end location)

These are available through [`YamlException.SourceName`](xref:SharpYaml.YamlException.SourceName), [`YamlException.Start`](xref:SharpYaml.YamlException.Start), and [`YamlException.End`](xref:SharpYaml.YamlException.End).

```csharp
var options = new YamlSerializerOptions
{
    SourceName = "appsettings.yaml",
};

var model = YamlSerializer.Deserialize<MyConfig>(": invalid", options);
```

## TryDeserialize

If you prefer failure-tolerant parsing without exceptions, use [`YamlSerializer.TryDeserialize(...)`](xref:SharpYaml.YamlSerializer.TryDeserialize``1(System.String,``0@,SharpYaml.YamlSerializerOptions)):

```csharp
if (!YamlSerializer.TryDeserialize<MyConfig>(yaml, out var model, options))
{
    // Invalid YAML or incompatible payload.
}
```

## Required members

If a required member is missing (for example [`YamlRequiredAttribute`](xref:SharpYaml.Serialization.YamlRequiredAttribute) or [`JsonRequiredAttribute`](xref:System.Text.Json.Serialization.JsonRequiredAttribute)), deserialization throws [`YamlException`](xref:SharpYaml.YamlException).
