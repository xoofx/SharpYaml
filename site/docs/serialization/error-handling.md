---
title: Errors and diagnostics
---

SharpYaml throws `YamlException` for parsing and (most) serialization failures.

## Parsing context

When possible, exceptions include:

- source name (for example a file path) via `YamlSerializerOptions.SourceName`
- line and column
- a span (start/end location)

```csharp
var options = new YamlSerializerOptions
{
    SourceName = "appsettings.yaml",
};

var model = YamlSerializer.Deserialize<MyConfig>(": invalid", options);
```

## Required members

If a required member is missing (for example `[YamlRequired]` or `[JsonRequired]`), deserialization throws `YamlException`.

