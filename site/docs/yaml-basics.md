---
title: YAML basics
---

YAML is a human-focused data format commonly used for configuration. In SharpYaml, most scenarios fall into one of these shapes:

- **Scalars**: strings, numbers, booleans, `null`
- **Mappings**: key/value pairs (like JSON objects)
- **Sequences**: ordered lists (like JSON arrays)

## Scalars

```yaml
name: Ada
age: 37
enabled: true
pi: 3.14159
missing: null
```

YAML has multiple ways to represent scalars:

- plain: `value`
- single-quoted: `'value'`
- double-quoted: `"value"`
- literal and folded block scalars:

```yaml
literal: |
  line 1
  line 2

folded: >
  line 1
  line 2
```

## Mappings (objects)

```yaml
server:
  host: localhost
  port: 8080
```

## Sequences (arrays/lists)

```yaml
names:
  - Ada
  - Bob
```

## Documents

A YAML stream can contain multiple documents separated by `---`:

```yaml
--- # doc 1
name: a
--- # doc 2
name: b
```

SharpYaml can parse a stream into a syntax tree/model. The object serializer ([`YamlSerializer`](xref:SharpYaml.YamlSerializer)) focuses on mapping one document into a .NET object.
