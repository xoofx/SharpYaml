---
title: Nodes and values
---

The model layer represents YAML content as nodes:

- `YamlValue` for scalar values
- `YamlMapping` for mappings (key/value pairs)
- `YamlSequence` for sequences (lists)

This is useful for:

- reading YAML without binding to a fixed CLR type
- building YAML programmatically
- representing extension data (`YamlExtensionData`) as a mapping

## Load and inspect YAML

```csharp
using System.IO;
using SharpYaml.Model;

var stream = YamlStream.Load(new StringReader("a: 1\nb: 2\n"));
var doc = stream[0];

var mapping = (YamlMapping)doc.Contents!;
var a = (YamlValue)mapping[new YamlValue("a")]!;

var value = a.Value; // "1"
```

## Author YAML programmatically

```csharp
using SharpYaml.Model;

var root = new YamlMapping
{
    { new YamlValue("name"), new YamlValue("Ada") },
    { new YamlValue("age"), new YamlValue(37) },
};

var doc = new YamlDocument { Contents = root };
var yaml = doc.ToString();
```

## Bridge to object mapping

The model layer has helper APIs that go through `YamlSerializer`:

```csharp
var element = YamlNode.FromObject(new { Name = "Ada" });
var model = element.ToObject<Person>();
```
