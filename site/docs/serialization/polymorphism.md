---
title: Polymorphism
---

SharpYaml supports polymorphism in a JSON-like way by default: using a discriminator property within a mapping.

It also supports YAML tags as an alternative discriminator style.

## JSON-like discriminator property

Use `JsonPolymorphicAttribute` and `JsonDerivedTypeAttribute`:

```csharp
using System.Text.Json.Serialization;

[JsonPolymorphic(TypeDiscriminatorPropertyName = "$type")]
[JsonDerivedType(typeof(Dog), "dog")]
[JsonDerivedType(typeof(Cat), "cat")]
public abstract class Animal
{
    public string Name { get; set; } = "";
}
```

When serialized, a discriminator property is emitted:

```yaml
$type: dog
Name: Rex
```

## YAML tag discriminator

Use `YamlPolymorphicAttribute` and `YamlDerivedTypeAttribute`:

```csharp
using SharpYaml.Serialization;

[YamlPolymorphic(DiscriminatorStyle = YamlTypeDiscriminatorStyle.Tag)]
[YamlDerivedType(typeof(Dog), "dog", Tag = "!dog")]
public abstract class TaggedAnimal
{
}
```

## Notes

- For safety, runtime activation from tag type names is disabled by default.
- When using tags, prefer an explicit registry (`YamlDerivedType`/`JsonDerivedType`) over free-form type names.

