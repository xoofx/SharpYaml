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
`Message` carries them as a prefix; [`YamlException.Reason`](xref:SharpYaml.YamlException.Reason) is the message without it, for reporting the
location in your own format.

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

By default the first rejected value ends the call. To collect every rejected value in one pass, or to keep the rest of the
document, see [Invalid values](#invalid-values).

## Invalid values

A converter that rejects a value, for example `abc` for an `int` member, throws a `YamlException` and the call fails at that
value. [`InvalidValueHandling`](xref:SharpYaml.YamlSerializerOptions.InvalidValueHandling) selects what happens instead:

| Mode | Behavior |
| --- | --- |
| [`FailFast`](xref:SharpYaml.YamlInvalidValueHandling.FailFast) (default) | The first invalid value throws, as before. |
| [`FailAtEnd`](xref:SharpYaml.YamlInvalidValueHandling.FailAtEnd) | Every recoverable value is skipped and the read continues. At the end the call throws one [`YamlInvalidValuesException`](xref:SharpYaml.YamlInvalidValuesException) listing all of them. |
| [`Skip`](xref:SharpYaml.YamlInvalidValueHandling.Skip) | Every recoverable value is skipped and the call succeeds. |

A skipped member keeps the value its initializer gave it, or its existing instance under
[`JsonObjectCreationHandling.Populate`](xref:System.Text.Json.Serialization.JsonObjectCreationHandling.Populate). A skipped constructor
parameter keeps its default value. The key still counts as present, so a required member whose value was skipped does not cause a
missing-member error, and a second occurrence of the key is still a duplicate.

A skipped collection element is dropped, so the collection holds the valid elements in document order. A skipped
dictionary value drops that entry. Its key still counts as seen, so a second occurrence of the key is still a
duplicate, and a merge key (`<<`) does not fill it in, regardless of its position in the mapping. An earlier valid
explicit value under `LastWins`, or an existing value in the dictionary being populated, is kept; a merged fallback is
removed when its explicit replacement is invalid. Dictionary keys are not recovered: a key that fails to convert ends
the call in every mode.

```csharp
var options = new YamlSerializerOptions
{
    SourceName = path,
    InvalidValueHandling = YamlInvalidValueHandling.FailAtEnd,
};

try
{
    return YamlSerializer.Deserialize<MyConfig>(yaml, options);
}
catch (YamlInvalidValuesException e)
{
    foreach (var value in e.InvalidValues)
    {
        logger.LogError("{File}({Line},{Column}): {Path}: {Message}",
            value.SourceName, value.ValueStart.Line + 1, value.ValueStart.Column + 1, value.Path, value.Message);
    }

    throw;
}
```

The exception is a `YamlException` located at the first skipped value, so `catch (YamlException)` and `TryDeserialize` keep working:
`TryDeserialize` returns `false` under `FailAtEnd` and `true` under `Skip`. Its message lists every value with its path.

### Observing skipped values

Under `FailAtEnd` and `Skip`, two delegates observe the skipped values, shaped like the delegates for
[unknown members](extension-data.md#handling-unknown-members-globally):

- [`InvalidValueCallback`](xref:SharpYaml.YamlSerializerOptions.InvalidValueCallback) runs once per skipped value, in document order.
- [`InvalidValuesFinalizer`](xref:SharpYaml.YamlSerializerOptions.InvalidValuesFinalizer) runs once at the end of the call with every
  skipped value, only when there is at least one. Under `FailAtEnd` it runs before the exception is thrown and can throw to replace it.

Neither runs under `FailFast`. Both receive [`YamlInvalidValue`](xref:SharpYaml.Serialization.YamlInvalidValue) instances:

| Property | Description |
| --- | --- |
| `SourceName` | `YamlSerializerOptions.SourceName` (for example the file path) |
| `Path` | Document path, for example `$.servers[2].port` (`$['a.b']` for keys that are not simple identifiers, `$[1]` for an element of a root sequence). An index is the position in the document, so once an element is dropped it no longer matches the deserialized collection |
| `KeyStart`, `ValueStart` | Line, column and index of the key and of its value. A sequence element has no key, so its `KeyStart` is the start of the element |
| `Name`, `TargetType` | The key and the CLR type of the member or constructor parameter the value was meant for. For a collection item, the zero-based document index (`"1"`) or the entry key, and the element or value type |
| `DeclaringType`, `Instance` | The CLR type and the object being populated (`null` for constructor-bound types). For a collection item, the collection type and the collection the item would have been added to (`null` for arrays and immutable collections, which are built afterwards) |
| `Value` | The rejected value as a `YamlElement`; `null` unless a callback or finalizer is set |
| `Message`, `Exception` | Why the value was rejected, without a location, and the `YamlException` the converter threw, whose `Start` and `End` locate the value |

```csharp
var options = new YamlSerializerOptions
{
    SourceName = path,
    InvalidValueHandling = YamlInvalidValueHandling.Skip,
    InvalidValueCallback = v => logger.LogWarning(
        "{File}({Line},{Column}): {Path}: {Message} The default is used.",
        v.SourceName, v.ValueStart.Line + 1, v.ValueStart.Column + 1, v.Path, v.Message),
};
```

### What is recoverable

A value is skipped when a `YamlException` is thrown while the reader is still on the first token of the value, that is before
any of it was consumed. This covers the built-in scalar converters, a shape mismatch such as a mapping where a scalar is
expected, and a custom converter that throws [`reader.CreateException(...)`](xref:SharpYaml.Serialization.YamlReader.CreateException(System.String))
before reading, see [Converters](converters.md#rejecting-a-value). Everything else stays fatal, in every mode:

- Other exception types. A `FormatException` from a converter is still wrapped in a `YamlException` and ends the call, so a
  converter bug is never reported as invalid content.
- An exception thrown after part of the value was consumed, for example by a nested object whose own member was fine.
- Parser errors (`SyntaxErrorException`, `SemanticErrorException`).
- The root value itself, dictionary keys and extension data. Collection elements and dictionary values are recovered
  like member values, inside a root collection too.
- Errors that no value read throws: `UnmappedMemberHandling = Disallow`, `DuplicateKeyHandling = Error`, a missing required
  member and polymorphic discriminator errors.

When such an error ends a read that already skipped values, `FailAtEnd` throws `YamlInvalidValuesException` with the skipped
values and the error as its `InnerException`, while `Skip` lets the error propagate unchanged. The finalizer does not run in
either case, because the read did not complete.

### Notes

- Options stay immutable and can be shared: the state of a deserialization lives in the reader, not in the options.
- Under `FailFast` there is no extra work. The other modes track the document path only when a report is consumed.
- Source-generated contexts are supported through the same options (`context.CreateOptions(o => o with { ... })`).
- Inside polymorphic nodes the `Path` is complete, but `KeyStart`, `ValueStart` and the exception marks are relative to the buffered node.
- Member values, constructor arguments, collection elements and dictionary values reached through a merge key (`<<`) are
  recovered like any other.
- A dropped element shifts the elements after it in the deserialized collection; `Path` and `Name` keep the positions in
  the document.
- Recovery applies to deserialization only. A converter that cannot write a value has a bug, not bad input.

## Required members

If a required member is missing (for example [`YamlRequiredAttribute`](xref:SharpYaml.Serialization.YamlRequiredAttribute) or [`JsonRequiredAttribute`](xref:System.Text.Json.Serialization.JsonRequiredAttribute)), deserialization throws [`YamlException`](xref:SharpYaml.YamlException).

## Unknown members

By default unknown keys are skipped. Set `UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow` to throw on the first one, or use
[`UnmappedMemberCallback` and `UnmappedMembersFinalizer`](extension-data.md#handling-unknown-members-globally) to log, suggest corrections or fail with all of them.
