---
title: Emitter and writer
---

The emitter writes YAML text from parsing events.

If you are using object mapping, you rarely need the emitter directly. The emitter is most useful for:

- tooling and transformations that operate on event streams
- building YAML output without binding to CLR types
- integrating SharpYaml into custom pipelines

## Emitting a mapping

The `Emitter` consumes `SharpYaml.Events.ParsingEvent` instances.

```csharp
using System.IO;
using SharpYaml;
using SharpYaml.Events;

var writer = new StringWriter();
var emitter = new Emitter(writer);

emitter.Emit(new StreamStart());
emitter.Emit(new DocumentStart());
emitter.Emit(new MappingStart());

emitter.Emit(new Scalar("name"));
emitter.Emit(new Scalar("Ada"));

emitter.Emit(new Scalar("age"));
emitter.Emit(new Scalar("37"));

emitter.Emit(new MappingEnd());
emitter.Emit(new DocumentEnd());
emitter.Emit(new StreamEnd());

var yaml = writer.ToString();
```

## Emitting a model

If you are using the model layer (`SharpYaml.Model`), you can write it back through the emitter:

```csharp
using SharpYaml.Model;

var stream = YamlStream.Load(new StringReader("a: 1\n"));
var doc = stream[0];

var sb = new StringWriter();
doc.WriteTo(sb);
```

## Formatting controls

Emission is influenced by options such as:

- indentation (`WriteIndented`, `IndentSize`)
- mapping ordering (`MappingOrder`)
- scalar style preferences (`ScalarStylePreferences`)

For object mapping, these are configured through `YamlSerializerOptions`. For low-level emission, the emitter has its own tuning knobs (canonical mode, indentation, width).
