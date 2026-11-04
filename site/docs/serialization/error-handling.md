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

## Exceptions thrown by converters

An exception thrown while a value is read, for example by the `Read` method of a custom [`YamlConverter<T>`](xref:SharpYaml.Serialization.YamlConverter`1)
or a deserialization callback, reaches the caller as a [`YamlException`](xref:SharpYaml.YamlException) with the original exception as its `InnerException`.
This is the same with reflection-based and source-generated metadata, so a single `catch (YamlException)`, or `TryDeserialize`, handles invalid content
however the converter reports it. A converter can still throw `YamlException` itself to choose the message and the location.

Two exceptions propagate unchanged because they are not about the content: `NotSupportedException`, for a configuration error
such as a type without a converter or a context [generated without readers](../source-generation.md#keeping-the-generated-code-small),
and `IOException`, for a failure of the source `TextReader` or `Stream`.

## Required members

If a required member is missing (for example [`YamlRequiredAttribute`](xref:SharpYaml.Serialization.YamlRequiredAttribute) or [`JsonRequiredAttribute`](xref:System.Text.Json.Serialization.JsonRequiredAttribute)), deserialization throws [`YamlException`](xref:SharpYaml.YamlException).

## Unknown members

By default unknown keys are skipped. Set `UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow` to throw on the first one, or use
[`UnmappedMemberCallback` and `UnmappedMembersFinalizer`](extension-data.md#handling-unknown-members-globally) to log, suggest corrections or fail with all of them.
