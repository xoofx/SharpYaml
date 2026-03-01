---
title: SharpYaml attributes
---

SharpYaml provides YAML-focused attributes in `SharpYaml.Serialization`. These complement the supported `System.Text.Json` attributes.

## Attribute reference

| Attribute | Target | Reflection | Source generated | Purpose |
| --- | --- | --- |
| `YamlPropertyNameAttribute` | Member | Yes | Yes | Overrides the serialized property name. |
| `YamlPropertyOrderAttribute` | Member | Yes | Yes | Controls ordering within a mapping. |
| `YamlIgnoreAttribute` | Member | Yes | Yes | Ignores a member for serialization/deserialization. |
| `YamlIncludeAttribute` | Member | Yes | Yes | Includes a non-public member when supported. |
| `YamlRequiredAttribute` | Member | Yes | Yes | Missing required members throw `YamlException`. |
| `YamlExtensionDataAttribute` | Member | Yes | Yes | Captures extra keys into a dictionary or `YamlMapping`. |
| `YamlConverterAttribute` | Type/Member | Yes | Yes | Assigns a custom converter. |
| `YamlConstructorAttribute` | Constructor | Yes | Yes | Selects which constructor to use for deserialization. |
| `YamlPolymorphicAttribute` | Type | Yes | Yes | Configures polymorphism (discriminator style). |
| `YamlDerivedTypeAttribute` | Type | Yes | Yes | Registers a derived type and optional tag/discriminator. |

## Precedence with JSON attributes

If you use both YAML and JSON attributes (for example `YamlPropertyName` and `JsonPropertyName`), YAML attributes win.
