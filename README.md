# SharpYaml [![ci](https://github.com/xoofx/SharpYaml/actions/workflows/ci.yml/badge.svg)](https://github.com/xoofx/SharpYaml/actions/workflows/ci.yml) [![NuGet](https://img.shields.io/nuget/v/SharpYaml.svg)](https://www.nuget.org/packages/SharpYaml/)

SharpYaml is a .NET YAML parser/emitter and object serializer.

SharpYaml v3 uses a `System.Text.Json`-style API:

- `YamlSerializer`
- `YamlSerializerOptions`
- `YamlTypeInfo` and resolver-based metadata (`IYamlTypeInfoResolver`)

## Target Framework

- Runtime: `net10.0`
- Tests: `net10.0`

## Basic Usage

```csharp
using SharpYaml;

var options = new YamlSerializerOptions
{
    PropertyNamingPolicy = YamlNamingPolicy.CamelCase,
    WriteIndented = true,
};

var yaml = YamlSerializer.Serialize(new { FirstName = "Ada", Age = 37 }, options);
var model = YamlSerializer.Deserialize<Person>(yaml, options);
```

## Reflection Control

Reflection fallback can be disabled globally before serializer first use:

```csharp
AppContext.SetSwitch("SharpYaml.YamlSerializer.IsReflectionEnabledByDefault", false);
```

When reflection is disabled, configure `YamlSerializerOptions.TypeInfoResolver` (for example with a generated `YamlSerializerContext`).

## Source Generation

Declare a context with `JsonSerializable` roots:

```csharp
using System.Text.Json.Serialization;
using SharpYaml.Serialization;

[JsonSerializable(typeof(MyConfig))]
internal partial class MyYamlContext : YamlSerializerContext
{
}
```

Then consume generated metadata:

```csharp
var context = new MyYamlContext();
var typeInfo = context.GetTypeInfo<MyConfig>();
var yaml = YamlSerializer.Serialize(config, typeInfo);
var roundTrip = YamlSerializer.Deserialize(yaml, typeInfo);
```

## Docs

- API reference: `site/api/sharpyaml3_api.md`
- v2 to v3 migration: `site/migration/v2_to_v3.md`
- v3 specification: `site/specs/sharpyaml3_specs.md`
- NativeAOT smoke sample: `src/SharpYaml.AotSmoke/`

## License

MIT
