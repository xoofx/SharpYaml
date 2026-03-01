---
title: JSON attribute interop
---

SharpYaml supports a subset of `System.Text.Json.Serialization` attributes to make it easy to reuse models across JSON and YAML.

## Supported JSON attributes

Member-level attributes:

| Attribute | Reflection | Source generated | Notes |
| --- | --- | --- |
| `JsonPropertyNameAttribute` | Yes | Yes | Overrides the serialized name. |
| `JsonIgnoreAttribute` | Yes | Yes | Supports `Always`, `Never`, `WhenWritingNull`, `WhenWritingDefault`. |
| `JsonIncludeAttribute` | Yes | Yes | Enables non-public members in supported scenarios. |
| `JsonPropertyOrderAttribute` | Yes | Yes | Controls member ordering within mappings. |
| `JsonRequiredAttribute` | Yes | Yes | Missing required members throw `YamlException`. |
| `JsonExtensionDataAttribute` | Yes | Yes | Supports dictionary- and mapping-based extension data. |

Type-level attributes:

| Attribute | Reflection | Source generated | Notes |
| --- | --- | --- |
| `JsonConstructorAttribute` | Yes | Yes | Selects which constructor to use for deserialization. |
| `JsonPolymorphicAttribute` | Yes | Yes | Supported via discriminator properties (JSON-like). |
| `JsonDerivedTypeAttribute` | Yes | Yes | Registers derived types and discriminators. |

## Unsupported JSON attributes

The following JSON attributes are not currently supported by SharpYaml:

| Attribute | Alternative |
| --- | --- |
| `JsonConverterAttribute` | Use `YamlConverterAttribute` or register converters via options. |
| Converter-specific attributes (number handling, etc.) | Use converters. |

## Precedence rules

When both YAML and JSON attributes are present, YAML-specific attributes take precedence.

## Examples

### Property naming

```csharp
using System.Text.Json.Serialization;

public sealed class Person
{
    [JsonPropertyName("first_name")]
    public string FirstName { get; set; } = "";
}
```

### Constructor selection

```csharp
using System.Text.Json.Serialization;

public sealed class Endpoint
{
    public string Url { get; }

    [JsonConstructor]
    public Endpoint(string url) => Url = url;
}
```
