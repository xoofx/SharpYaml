---
title: YAML 1.2.2 and schemas
---

SharpYaml targets the [YAML 1.2.2 specification](https://yaml.org/spec/1.2.2/) and exposes schema control via [`YamlSerializerOptions.Schema`](xref:SharpYaml.YamlSerializerOptions.Schema).

Schemas influence how plain scalars are resolved:

- `"true"` can be a boolean
- `"123"` can be an integer
- `"null"` can be null

In configuration scenarios, schema choices are often a tradeoff between convenience and strictness.

## Schema selection

```csharp
using SharpYaml;

var options = new YamlSerializerOptions
{
    Schema = YamlSchemaKind.Core,
    UseSchema = true,
};
```

Core resolution recognizes the spellings in specification section 10.3.2. Decimal integers may have
leading zeroes; octal and hexadecimal use `0o` and `0x`. Binary prefixes, numeric underscores, signed
octal/hexadecimal prefixes and arbitrary mixed-case booleans are strings, rather than implicit numbers
or booleans. Non-specific `!` forces a scalar to be a string. Explicit standard tags such as
`!!str`, `!!int` and `!!float` are honored by untyped loading, including on quoted values.
Default untyped integers use `Int64`, then `UInt64`, and then `BigInteger` when the value exceeds both
64-bit ranges. Explicit `!!int` uses the same default CLR widths. `UseSchema = true` retains the schema's
smallest native width. Large integers remain exact and can be serialized again, including with generated
metadata and NativeAOT.
With the default scalar-style preferences, writers quote numeric-looking strings, including large
hexadecimal and octal identifiers, so untyped round trips preserve their text and string type.

By default, untyped `object` values use a span-based Core resolver. Typed numeric conversion helpers
also accept convenient .NET spellings, including numeric underscores. Set `UseSchema = true` to
resolve typed values through the selected schema. `Extended` retains additional scalar forms for
applications that need them. The JSON schema does not implicitly resolve `.inf` or `.nan` as numbers.

## Parsing and conformance tests

The parser handles streams independently of reflection or generated type metadata. Regression tests
check all 402 enabled cases in the pinned official YAML Test Suite snapshot, including 94 invalid
streams, through both the string and streaming buffers. They compare exact events, scalar contents,
tags, anchors and document boundaries. Valid graphs also have decoded-value tests against upstream
JSON where available. Passing this corpus is evidence of coverage, not a proof for every possible input.

The fixture's revision, license and regeneration procedure are recorded alongside the tests.
The four upstream `ZYU8` cases marked `skip: true` are excluded, matching upstream's own generator.
Some third-party reports include those skipped cases; their totals are therefore different.

Only CR, LF and CRLF are YAML line breaks. NEL, Unicode line separator and paragraph separator remain
scalar content. Reserved directives are ignored, future YAML 1.x minor versions use the supported
grammar, and incompatible major versions are rejected. A directives document following a document
requires an explicit `...` end marker.

Flow collections inside a block require all continuation lines, including closing `]` and `}`, to be
indented more than that block (section 7.4). Earlier versions accepted a closing indicator at the parent's
indentation. This is now a syntax error with a flow-indentation diagnostic. For example:

```yaml
key: [
  first,
  second
  ]
```

Writers quote strings and keys containing U+FEFF so they cannot be mistaken for a byte-order mark.

`YamlSerializer.Deserialize` loads one document. Use the parser, reader or `YamlStream` when processing
a stream of documents. The mutable model expands aliases as copies and does not represent cyclic graphs.

## Mapping keys

Untyped mappings with scalar keys load as `Dictionary<string, object?>`. Scalar keys retain their text,
including the empty string for an omitted key. If a mapping contains a sequence or mapping key, its
result is `Dictionary<object, object?>`; scalar keys in that dictionary remain strings. Complex keys
compare by object identity, so enumerate them rather than looking them up with a newly created list
or dictionary. This avoids recursive hashing and permits keys whose contents are still being loaded.
Under reference preservation, a mapping cannot be promoted after an earlier alias has already
obtained its original string-keyed instance; this produces `YamlException` instead of leaving a stale
reference. Recursive aliases used as complex keys cannot be expanded as copies.

Typed dictionaries use the converter for their key type. Sequence and mapping keys can also be written,
using explicit YAML `?` keys, through reflection or generated metadata. String-keyed dictionaries and
CLR object members require scalar keys. Custom writers can use `WriteStartMappingKey`, write one node,
then call `WriteEndMappingKey` before writing its value.

## Duplicate keys

Object loading rejects duplicate scalar keys by default. Set `FirstWins` or `LastWins` when accepting
duplicates is intentional. Syntax tests can accept a mapping's event stream even when this loading
policy rejects its duplicate keys:

```csharp
var options = new YamlSerializerOptions
{
    DuplicateKeyHandling = YamlDuplicateKeyHandling.Error,
};
```

## Merge keys (`<<`)

SharpYaml supports the YAML 1.1 merge key (`<<`) as an extension when deserializing into .NET objects and dictionaries.
This behavior is enabled for [`YamlSchemaKind.Core`](xref:SharpYaml.YamlSchemaKind.Core) and [`YamlSchemaKind.Extended`](xref:SharpYaml.YamlSchemaKind.Extended) (it is ignored for [`YamlSchemaKind.Json`](xref:SharpYaml.YamlSchemaKind.Json) and [`YamlSchemaKind.Failsafe`](xref:SharpYaml.YamlSchemaKind.Failsafe) schemas).

## Notes

- The syntax layer is designed for tooling and lossless roundtrip.
- The object-mapping layer is optimized for configuration and does not preserve formatting/comments.
