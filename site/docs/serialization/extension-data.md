---
title: Extension data
---

Extension data captures unknown keys during deserialization.

This is useful for:

- forward-compatible configuration formats
- preserving unrecognized settings when a model is partial

## Dictionary-based extension data

```csharp
using SharpYaml.Serialization;

public sealed class Config
{
    public int Known { get; set; }

    [YamlExtensionData]
    public Dictionary<string, object?>? Extra { get; set; }
}
```

## Mapping-based extension data

If you need a YAML-native representation (for example to preserve YAML shapes), use `SharpYaml.Model.YamlMapping`:

```csharp
using SharpYaml.Model;
using SharpYaml.Serialization;

public sealed class Config
{
    [YamlExtensionData]
    public YamlMapping? Extra { get; set; }
}
```

