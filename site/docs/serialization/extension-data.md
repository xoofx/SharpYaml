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

If the extension data property is <see langword="null"/>, SharpYaml will create a new dictionary instance when unknown keys are encountered.
If no unknown keys are encountered, the property is left as <see langword="null"/>.

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

## Handling unknown members globally

Extension data needs a member on every type of your object graph.
To observe unknown keys anywhere in the graph, without changing your types, set
[`UnmappedMemberCallback`](xref:SharpYaml.YamlSerializerOptions.UnmappedMemberCallback) and/or
[`UnmappedMembersFinalizer`](xref:SharpYaml.YamlSerializerOptions.UnmappedMembersFinalizer) on the options:

- The **callback** runs once per unknown key, as it is encountered.
- The **finalizer** runs once at the end of a `YamlSerializer.Deserialize` call with every unknown key, only when there is at least one. It can throw to fail the call.

Both receive [`YamlUnmappedMember`](xref:SharpYaml.Serialization.YamlUnmappedMember) instances:

| Property | Description |
| --- | --- |
| `SourceName` | `YamlSerializerOptions.SourceName` (for example the file path) |
| `Path` | Document path, for example `$.servers[2].tls.colour` (`$['a.b']` for keys that are not simple identifiers) |
| `KeyStart`, `ValueStart` | Line, column and index of the key and of its value |
| `Name`, `Value` | The key and its value as a `YamlElement` |
| `DeclaringType`, `Instance` | The CLR type and the object being populated (`null` for constructor-bound types) |
| `KnownMemberNames`, `SuggestedName` | The names the type accepts, and the closest one when the key looks like a misspelling |
| `IsCapturedByExtensionData` | `true` when a `[YamlExtensionData]` member stored the key as well |

The callback runs for skipped keys and for keys captured by extension data.
With `UnmappedMemberHandling = Disallow` the first unknown key still throws, and neither delegate runs.

### Warn with a suggestion

```csharp
var options = new YamlSerializerOptions
{
    SourceName = path,
    UnmappedMemberCallback = m => logger.LogWarning(
        "{File}({Line},{Column}): unknown key '{Key}' at {Path}.{Suggestion}",
        m.SourceName, m.KeyStart.Line + 1, m.KeyStart.Column + 1, m.Name, m.Path,
        m.SuggestedName is { } suggestion ? $" Did you mean '{suggestion}'?" : string.Empty),
};
```

### Fail with every unknown key at once

`Disallow` stops at the first unknown key. A finalizer can report all of them:

```csharp
var options = new YamlSerializerOptions
{
    SourceName = path,
    UnmappedMembersFinalizer = members => throw new YamlException(
        members[0].SourceName,
        members[0].KeyStart,
        members[0].ValueStart,
        string.Join("\n", members.Select(m => $"{m.Path}: unknown key '{m.Name}'"))),
};
```

### Accept a legacy or misspelled key

```csharp
var options = new YamlSerializerOptions
{
    UnmappedMemberCallback = m =>
    {
        if (m.Instance is Theme theme && m.Name == "colour")
        {
            theme.Color = m.Value?.ToObject<string>();
        }
    },
};
```

### Notes

- Options stay immutable and can be shared: the state of a deserialization lives in the reader, not in the options.
- When neither delegate is set there is no extra work apart from a null check where unknown keys are handled.
- Source-generated contexts are supported through the same options (`context.CreateOptions(o => o with { ... })`).
- Inside polymorphic nodes the `Path` is complete, but `KeyStart`/`ValueStart` are relative to the buffered node.
- Aliases inside an unknown value that refer to anchors outside of it are represented as a `*name` value.
- The finalizer is invoked by the `YamlSerializer.Deserialize`/`TryDeserialize` methods, not when using `YamlReader` directly.
