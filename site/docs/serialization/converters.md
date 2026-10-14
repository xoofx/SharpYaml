---
title: Converters
---

Converters allow custom serialization/deserialization for specific CLR types.

## Registration methods

SharpYaml supports three ways to register converters, evaluated in this priority order:

1. **Member-level attribute** — highest priority, applies to a single property or field
2. **Options-level** — applies globally to all types the converter can handle
3. **Type-level attribute** — lowest priority of the three explicit registrations

If none of these match, SharpYaml falls through to built-in converters (primitives, collections, enums, etc.) and finally to the reflection-based object converter.

### Options-level registration

Register converters globally via [`YamlSerializerOptions.Converters`](xref:SharpYaml.YamlSerializerOptions.Converters). Converters are evaluated in order and take precedence over built-in converters:

```csharp
var options = new YamlSerializerOptions
{
    Converters =
    [
        new IPAddressConverter(),
        new TemperatureConverterFactory(),
    ],
};
```

### Type-level attribute

Apply [`YamlConverterAttribute`](xref:SharpYaml.Serialization.YamlConverterAttribute) to a type to associate a converter with all instances of that type:

```csharp
[YamlConverter(typeof(TemperatureConverter))]
public readonly struct Temperature
{
    public double Value { get; init; }
    public string Unit { get; init; }
}
```

### Member-level attribute

Apply [`YamlConverterAttribute`](xref:SharpYaml.Serialization.YamlConverterAttribute) to a property or field to override the converter for that specific member:

```csharp
public sealed class Config
{
    [YamlConverter(typeof(HexIntConverter))]
    public int Color { get; set; }

    // Uses the default int converter
    public int Count { get; set; }
}
```

## Converter shape

Converters extend [`YamlConverter<T>`](xref:SharpYaml.Serialization.YamlConverter`1) and operate on [`YamlReader`](xref:SharpYaml.Serialization.YamlReader) and [`YamlWriter`](xref:SharpYaml.Serialization.YamlWriter):

```csharp
public sealed class TemperatureConverter : YamlConverter<Temperature>
{
    public override Temperature Read(YamlReader reader)
    {
        var text = reader.ScalarValue!;
        reader.Read();

        var unit = text[^1..];
        var value = double.Parse(text[..^1], CultureInfo.InvariantCulture);
        return new Temperature { Value = value, Unit = unit };
    }

    public override void Write(YamlWriter writer, Temperature value)
        => writer.WriteScalar($"{value.Value.ToString("G", CultureInfo.InvariantCulture)}{value.Unit}");
}
```

## Converter factories

For open generic types or families of types, extend [`YamlConverterFactory`](xref:SharpYaml.Serialization.YamlConverterFactory):

```csharp
public sealed class NullableConverterFactory : YamlConverterFactory
{
    public override bool CanConvert(Type typeToConvert)
        => Nullable.GetUnderlyingType(typeToConvert) is not null;

    public override YamlConverter CreateConverter(Type typeToConvert, YamlSerializerOptions options)
    {
        var innerType = Nullable.GetUnderlyingType(typeToConvert)!;
        var converterType = typeof(NullableConverter<>).MakeGenericType(innerType);
        return (YamlConverter)Activator.CreateInstance(converterType)!;
    }
}
```

Register factories the same way as regular converters:

```csharp
var options = new YamlSerializerOptions
{
    Converters = [new NullableConverterFactory()],
};
```

## Reader and writer basics

- [`YamlReader`](xref:SharpYaml.Serialization.YamlReader) is positioned on a token; converters must consume the current value and advance the reader.
- [`YamlWriter`](xref:SharpYaml.Serialization.YamlWriter) writes YAML in a streaming manner; converters should write a complete value (scalar/sequence/mapping).

For most custom scenarios, prefer writing scalars (`writer.WriteScalar(...)`) unless you need to emit complex YAML structures.

### Requesting a scalar style

Use `WriteScalar(value, ScalarStyle)` to select a presentation style in a custom converter:

```csharp
using SharpYaml;
using SharpYaml.Serialization;

public sealed class LiteralStringConverter : YamlConverter<string>
{
    public override string? Read(YamlReader reader)
    {
        var value = reader.ScalarValue;
        reader.Read();
        return value;
    }

    public override void Write(YamlWriter writer, string? value)
        => writer.WriteScalar(value, ScalarStyle.Literal);
}
```

The available styles are `Any` (the usual `WriteScalar` behavior), `Plain`, `SingleQuoted`,
`DoubleQuoted`, `Literal` (`|`), and `Folded` (`>`). This overload writes **values**, not keys;
use `WritePropertyName` for mapping keys. A null value always writes plain `null`, regardless
of the requested style. An undefined enum value throws `ArgumentOutOfRangeException`, even
when the value is null.

Style requests preserve the scalar text rather than forcing an unsafe representation:

- Plain text that needs quoting falls back to double quotes. Plain style still allows YAML
  type resolution: for example, `"true"` written plain is a boolean to an untyped reader.
- Single quotes are doubled within single-quoted scalars. Control characters and line breaks
  use double quotes and escapes instead.
- Block styles preserve LF newlines and choose strip (`-`), clip, or keep (`+`) chomping to
  preserve trailing newlines. Folded style inserts extra blank lines where needed: `"a\nb"`
  becomes `>-` with an empty physical line between `a` and `b`.
- Block styles fall back to double quotes for unsafe characters and non-LF line breaks
  (including CRLF), avoiding YAML newline normalization. An ambiguous initial indentation
  also falls back when its required indicator cannot fit YAML's single-digit range, 1–9.

Text must contain valid Unicode. Unpaired UTF-16 surrogates throw `ArgumentException` rather
than producing invalid YAML; valid surrogate pairs (such as emoji) are preserved. As with
other streaming write failures, output may already have been written when an exception occurs.

An explicit style takes precedence over the global
[`MultilineStringStyle`](overview.md#multiline-string-style) preference. The low-level
`Emitter` also supports scalar style requests and uses quoted styles where block scalars
are not legal, such as flow collections and simple keys.

## Accessing the current mapping key

[`YamlReader.CurrentKey`](xref:SharpYaml.Serialization.YamlReader.CurrentKey) exposes the most recent mapping key set by built-in dictionary and object converters. Custom converters can use this for context-dependent logic:

```csharp
public sealed class KeyAwareConverter : YamlConverter<string>
{
    public override string? Read(YamlReader reader)
    {
        var text = reader.ScalarValue ?? string.Empty;
        reader.Read();

        // Replace placeholder with the dictionary key or property name
        return text.Replace("${KEY}", reader.CurrentKey ?? string.Empty);
    }

    public override void Write(YamlWriter writer, string? value)
        => writer.WriteScalar(value ?? string.Empty);
}
```

## Precedence summary

| Source | Scope | Priority |
| --- | --- | --- |
| `[YamlConverter]` on member | Single property/field | Highest |
| `YamlSerializerOptions.Converters` | All matching types | High |
| `[YamlConverter]` on type | All instances of type | Medium |
| Built-in converters | Primitives, collections, enums, etc. | Low |
| `IParsable<T>` fallback (.NET 7+) | Types implementing `IParsable<T>` | Lower |
| Object converter | Any remaining type | Lowest |
