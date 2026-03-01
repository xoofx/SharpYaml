---
title: SharpYaml attributes
---

SharpYaml provides YAML-focused attributes in `SharpYaml.Serialization`. These complement the supported `System.Text.Json` attributes.

## Attribute reference

| Attribute | Target | Purpose |
| --- | --- | --- |
| `YamlPropertyNameAttribute` | Member | Overrides the serialized property name. |
| `YamlPropertyOrderAttribute` | Member | Controls ordering within a mapping. |
| `YamlIgnoreAttribute` | Member | Ignores a member for serialization/deserialization. |
| `YamlIncludeAttribute` | Member | Includes a non-public member when supported. |
| `YamlRequiredAttribute` | Member | Missing required members throw `YamlException`. |
| `YamlExtensionDataAttribute` | Member | Captures extra keys into a dictionary or `YamlMapping`. |
| `YamlConverterAttribute` | Type/Member | Assigns a custom converter. |
| `YamlConstructorAttribute` | Constructor | Selects which constructor to use for deserialization. |
| `YamlPolymorphicAttribute` | Type | Configures polymorphism (discriminator style). |
| `YamlDerivedTypeAttribute` | Type | Registers a derived type and optional tag/discriminator. |

## Precedence with JSON attributes

If you use both YAML and JSON attributes (for example `YamlPropertyName` and `JsonPropertyName`), YAML attributes win.

