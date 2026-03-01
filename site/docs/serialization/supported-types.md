---
title: Supported types
---

This page describes what SharpYaml can serialize/deserialize out of the box.

SharpYaml always supports:

- `null`
- scalars (strings, numbers, booleans)
- sequences (arrays/lists)
- mappings (objects and dictionaries)

## Primitive CLR types

The built-in scalar converters support the following C# primitive types:

- `bool`
- `byte`, `sbyte`
- `short`, `ushort`
- `int`, `uint`
- `long`, `ulong`
- `nint`, `nuint`
- `float`, `double`
- `decimal`
- `char`
- `string`
- `DateTime`, `DateTimeOffset` (roundtrip/ISO-8601)
- `DateOnly`, `TimeOnly` (ISO-8601, available on .NET 6+)
- `Guid`
- `TimeSpan`
- `Half` (available on .NET 5+)
- `Int128`, `UInt128` (available on .NET 7+)
- enums (by name; numeric forms are accepted when possible)
- nullable forms of the above (`T?`)

### YAML numeric syntax

Integer parsing follows YAML 1.2 core conventions and supports:

- underscores as digit separators: `1_000_000`
- base prefixes: `0xFF`, `0o755`, `0b1010`
- optional sign: `+1`, `-1`

Floating-point parsing supports:

- `+/- .inf`
- `.nan`

## Collections

Common collection shapes are supported:

- arrays (`T[]`)
- `List<T>`
- `Dictionary<TKey, TValue>` (keys are typically scalars)

## Custom types

For custom types, SharpYaml uses an object converter that maps members to mapping keys. You can control this with:

- naming policies (`PropertyNamingPolicy`)
- JSON/YAML attributes (for example `[JsonPropertyName]`, `[YamlPropertyName]`)
- converters (`YamlConverter<T>` and `YamlConverterAttribute`)
