---
title: SharpYaml 3 specification
discard: true
---

# SharpYaml 3 (SharpYaml3) Specification

Last updated: 2026-02-28
Status: Draft (implementation plan)

## 1. Background / Motivation

SharpYaml is a legacy YAML parser + object serializer (historically forked from YamlDotNet). The existing public surface and internal architecture predate modern .NET patterns:

- Serialization API is not familiar to developers used to `System.Text.Json` (`JsonSerializer`, `JsonSerializerOptions`, source-gen contexts, etc.).
- NativeAOT and aggressive trimming require a metadata-first design and a source generator to avoid reflection at runtime.
- Attribute ecosystem has drifted: users expect `System.Text.Json.Serialization` attributes to “just work” (e.g. `[JsonPropertyName]`) across dynamic and generated serialization.
- Performance opportunities exist (`ReadOnlySpan<char>`, fewer allocations, better pooling).
- Low-level YAML infrastructure should support lossless roundtrip and accurate source spans (syntax highlighting, diagnostics), separate from .NET object mapping.
- YAML 1.2 compatibility and conformance coverage can be improved, and the test suite needs a modern refresh.

This document specifies the SharpYaml “v3” breaking-change rewrite that becomes the long-term foundation for YAML parsing/emitting and modern serialization.

## 2. Non-Goals (To Keep Scope Realistic)

These are explicitly out-of-scope for v3.0 unless later promoted:

- Lossless roundtrip when mapping YAML <-> .NET objects via `YamlSerializer` (comments/formatting/style preservation).
- Full feature parity with all YAML processors.
- A complete 1:1 mirror of every `System.Text.Json` behavior and all attributes (especially converter-specific behaviors).
- Implementing every YAML 1.2 edge case in the first iteration without an explicit test driving it.

## 3. Versioning, Packaging, and Target Frameworks

### 3.1 Package identity

- NuGet package remains `SharpYaml` but bumps to **major version 3.0.0**.
- Assembly name remains `SharpYaml` (unless a separate package is required for compatibility shims).

### 3.2 Target frameworks

- Runtime library: `net8.0`, `net10.0`, `netstandard2.0`.
- Tests: **`net10.0` only**.
- Source generator (analyzers): `netstandard2.0` (preferred) or the minimum required by the chosen Roslyn package versions.

### 3.3 Tooling pin

- Update `src/global.json` to pin a .NET 10 SDK (initially `10.0.100` unless the repo standardizes on a later `10.0.1xx`).

### 3.4 Breaking changes policy

- Remove legacy serialization API types that are incompatible with the new design (`SharpYaml.Serialization.Serializer`, `SerializerSettings`, `IYamlSerializable`, etc.).
- Provide a short migration guide in `site/` describing common replacements.

## 4. Public API Design (System.Text.Json-like)

### 4.1 Primary entrypoint

Introduce a `YamlSerializer` static class analogous to `System.Text.Json.JsonSerializer`:

```csharp
namespace SharpYaml;

public static class YamlSerializer
{
    // Similar to System.Text.Json.JsonSerializer.IsReflectionEnabledByDefault (controlled via AppContext switch).
    public static bool IsReflectionEnabledByDefault { get; }

    public static string Serialize<T>(T value, YamlSerializerOptions? options = null);
    public static string Serialize(object? value, Type inputType, YamlSerializerOptions? options = null);

    public static void Serialize<T>(TextWriter writer, T value, YamlSerializerOptions? options = null);
    public static void Serialize(TextWriter writer, object? value, Type inputType, YamlSerializerOptions? options = null);

    public static T? Deserialize<T>(string yaml, YamlSerializerOptions? options = null);
    public static object? Deserialize(string yaml, Type returnType, YamlSerializerOptions? options = null);

    // Span<char> and streaming (configuration-first; UTF-16 text is the primary surface in v3):
    public static T? Deserialize<T>(TextReader reader, YamlSerializerOptions? options = null);
    public static object? Deserialize(TextReader reader, Type returnType, YamlSerializerOptions? options = null);
    public static T? Deserialize<T>(ReadOnlySpan<char> yaml, YamlSerializerOptions? options = null);
    public static object? Deserialize(ReadOnlySpan<char> yaml, Type returnType, YamlSerializerOptions? options = null);

    // TypeInfo overloads (source-gen path):
    public static string Serialize<T>(T value, YamlTypeInfo<T> typeInfo);
    public static T? Deserialize<T>(string yaml, YamlTypeInfo<T> typeInfo);
    public static T? Deserialize<T>(ReadOnlySpan<char> yaml, YamlTypeInfo<T> typeInfo);
}
```

Notes:

- The `TypeInfo` overloads are the preferred path for NativeAOT.
- The recommended primitives for new code are `string` / `ReadOnlySpan<char>` plus `TextReader` / `TextWriter` where appropriate.
- Reflection fallback can be disabled globally before first use via:
  - `AppContext.SetSwitch("SharpYaml.YamlSerializer.IsReflectionEnabledByDefault", false);`
- `YamlSerializer` is a .NET object mapper and does not preserve YAML formatting/comments for lossless roundtrip. Use the low-level syntax APIs for roundtrippable scenarios (section 8.3).

### 4.2 Options object

Introduce `YamlSerializerOptions` analogous to `JsonSerializerOptions`, with a similar naming strategy where it makes sense for YAML:

```csharp
namespace SharpYaml;

public sealed class YamlSerializerOptions
{
    public static YamlSerializerOptions Default { get; }

    // Naming
    public YamlNamingPolicy? PropertyNamingPolicy { get; set; }
    public YamlNamingPolicy? DictionaryKeyPolicy { get; set; }
    public bool PropertyNameCaseInsensitive { get; set; } // default: false

    // Null / default handling
    public YamlIgnoreCondition DefaultIgnoreCondition { get; set; } // Never/WhenWritingNull/WhenWritingDefault

    // Formatting
    public bool WriteIndented { get; set; }
    public int IndentSize { get; set; } // default: 2

    // Mapping/member ordering
    public YamlMappingOrderPolicy MappingOrder { get; set; } // default: Declaration

    // YAML specifics
    public YamlSchemaKind Schema { get; set; } // default: Core (YAML 1.2 core)
    public YamlDuplicateKeyHandling DuplicateKeyHandling { get; set; } // Error / LastWins / FirstWins
    public YamlScalarStylePreferences ScalarStylePreferences { get; set; }

    // Polymorphism
    public YamlPolymorphismOptions PolymorphismOptions { get; }

    // References (anchors/aliases)
    public YamlReferenceHandling ReferenceHandling { get; set; } // None / Preserve

    // Metadata / resolvers
    public IYamlTypeInfoResolver? TypeInfoResolver { get; set; }
}
```

Define ordering policy:

```csharp
namespace SharpYaml;

public enum YamlMappingOrderPolicy
{
    Declaration = 0,
    Sorted = 1,
}
```

Key defaults:

- Default schema is **YAML 1.2 Core**.
- Reflection fallback is **enabled by default** for familiarity.
  - Reflection paths must be marked `[RequiresUnreferencedCode]` / `[RequiresDynamicCode]` as appropriate.
  - NativeAOT apps should provide generated metadata via `TypeInfoResolver` and disable reflection fallback via `YamlSerializer.IsReflectionEnabledByDefault`.
  - Even when reflection is disabled, primitives and untyped containers (`object`, `Dictionary<string, object>`, `List<object>`, `object[]`) should remain supported out of the box.

### 4.3 Type metadata model (for both dynamic + generated)

Implement a metadata model similar to `JsonTypeInfo`:

```csharp
namespace SharpYaml.Serialization.Metadata;

public abstract class YamlTypeInfo
{
    public Type Type { get; }
    public YamlTypeKind Kind { get; } // Object / Dictionary / Enumerable / Scalar
}

public abstract class YamlTypeInfo<T> : YamlTypeInfo
{
    // Strongly typed helpers for generated code.
}

public interface IYamlTypeInfoResolver
{
    YamlTypeInfo? GetTypeInfo(Type type, YamlSerializerOptions options);
}
```

Design rules:

- **Single pipeline**: `YamlSerializer` always works from `YamlTypeInfo` obtained via `TypeInfoResolver` (generator or reflection).
- `YamlSerializerOptions.TypeInfoResolver` can be:
  - A generated context (`YamlSerializerContext`).
  - A reflection resolver (provided by SharpYaml).
  - A composite resolver that tries generated first then reflection.

### 4.4 Source-generation API

Expose a generator-friendly API similar to STJ, but **do not require tagging model types**.

Source generation is driven by a *context class* (like `JsonSerializerContext`). Types are declared on the context using the existing `System.Text.Json.Serialization.JsonSerializableAttribute` so users can keep a familiar workflow and often reuse their existing lists of types.

```csharp
using System.Text.Json.Serialization;
using SharpYaml.Serialization;

[YamlSourceGenerationOptions(WriteIndented = true)]
[JsonSerializable(typeof(MyConfig))]
[JsonSerializable(typeof(List<MyItem>))]
internal partial class MyYamlContext : YamlSerializerContext
{
}
```

`YamlSerializerContext` is a SharpYaml base type that:

- Implements `IYamlTypeInfoResolver` for `YamlSerializerOptions.TypeInfoResolver`.
- Provides the configured `YamlSerializerOptions`.

The generator must honor `YamlSourceGenerationOptionsAttribute` by mapping it into the generated context's default `YamlSerializerOptions`.

For convenience (and parity with `System.Text.Json`), the generator may also honor the overlapping subset of `JsonSourceGenerationOptionsAttribute`.

The overlapping subset maps as follows:

- `WriteIndented` -> `YamlSerializerOptions.WriteIndented`
- `IndentSize` -> `YamlSerializerOptions.IndentSize`
- `PropertyNameCaseInsensitive` -> `YamlSerializerOptions.PropertyNameCaseInsensitive`
- `DefaultIgnoreCondition` -> `YamlSerializerOptions.DefaultIgnoreCondition` (mapped from `JsonIgnoreCondition`)
- `PropertyNamingPolicy` -> `YamlSerializerOptions.PropertyNamingPolicy` (mapped from `JsonKnownNamingPolicy`)
- `DictionaryKeyPolicy` -> `YamlSerializerOptions.DictionaryKeyPolicy` (mapped from `JsonKnownNamingPolicy`)

YAML-specific settings (e.g. schema selection, duplicate key handling, scalar style preferences, reference handling, polymorphism defaults, converter registration) are available on `YamlSourceGenerationOptionsAttribute` so they can be fixed at build time for NativeAOT-friendly contexts.

`YamlSerializerContext` base type shape:

```csharp
namespace SharpYaml.Serialization;

public abstract partial class YamlSerializerContext : IYamlTypeInfoResolver
{
    protected YamlSerializerContext(YamlSerializerOptions options) => Options = options;
    public YamlSerializerOptions Options { get; }
}
```

Generated context should provide:

- `public static <ContextName> Default { get; }` (or `Instance`) with fixed options.
- `public YamlTypeInfo<T> <TName> { get; }` properties for each `[JsonSerializable]` entry.

## 5. Attribute Support (JSON + YAML)

### 5.1 Principles

- YAML-specific attributes take precedence over JSON attributes when both are present.
- JSON attributes are supported **out of the box** in both reflection and source-gen paths (when feasible).
- Attributes that only make sense for JSON converters are not required for v3.0.

### 5.2 YAML attribute set (new or revised)

Introduce/standardize the following attributes for v3:

- `[YamlPropertyName(string name)]`: rename a property/field.
- `[YamlIgnore]`: ignore a property/field.
- `[YamlInclude]`: include non-public members when explicitly opted in.
- `[YamlPropertyOrder(int order)]`: stable ordering in emitted mappings (when enabled).
- `[YamlConstructor]`: mark the preferred constructor.
- `[YamlPolymorphic(...)]` and `[YamlDerivedType(...)]`: YAML-native polymorphism (see section 6).

Legacy attributes (`YamlMemberAttribute`, `YamlTagAttribute`, `YamlIgnoreAttribute`, `YamlStyleAttribute`, etc.) policy:

- v3.0 is a clean break: remove the legacy attribute set and do not carry compatibility shims in the codebase.
- Provide a migration guide mapping legacy attributes to their v3 equivalents where possible.

### 5.3 JSON attributes supported

Support at minimum:

- `[System.Text.Json.Serialization.JsonPropertyName]` -> same behavior as `[YamlPropertyName]`.
- `[JsonIgnore]` -> same behavior as `[YamlIgnore]` for read/write.
  - For `JsonIgnoreCondition`, map as:
    - `Always` => ignore always.
    - `WhenWritingNull` => ignore on write when null.
    - `WhenWritingDefault` => ignore on write when default.
- `[JsonInclude]` -> include a non-public property/field when enabled.
- `[JsonPropertyOrder]` -> map to YAML order.
- `[JsonExtensionData]` -> map to “extra mapping members” (see section 7.6).
- `[JsonConstructor]` -> select constructor for deserialization.
- `[JsonDerivedType]` and `[JsonPolymorphic]` -> map into YAML polymorphism model (section 6).

Not required for v3.0:

- `[JsonConverter]` support (unless explicitly designed as a compatibility layer).
- Number handling attributes (unless needed by tests).

### 5.4 Precedence rules (must be deterministic)

For each member:

1. YAML-specific attributes (`YamlPropertyName`, `YamlIgnore`, etc.)
2. JSON attributes (`JsonPropertyName`, `JsonIgnore`, etc.)
3. Options policies (`PropertyNamingPolicy`, ignore conditions, etc.)

Conflicts:

- If both YAML and JSON rename attributes exist, YAML wins.
- If both YAML and JSON ignore attributes exist, YAML wins.
- If `PropertyNamingPolicy` is set, it applies only when no rename attribute exists.

## 6. Polymorphism / Derived Types

### 6.1 Goals

- Provide a first-class, declarative way to support derived types for:
  - Reflection-based serialization.
  - Source-generated serialization (NativeAOT-friendly).
- Allow users to reuse `JsonDerivedType`/`JsonPolymorphic` attributes without rewriting models.

### 6.2 Discriminator strategies in YAML

YAML has native tags, but many YAML documents use “kind/type” members. Support both:

- **Tag-based**: `!dog` / `!!mytag` on the node.
- **Property-based**: a mapping member, e.g. `kind: dog`.

Define:

```csharp
public sealed class YamlPolymorphismOptions
{
    public YamlTypeDiscriminatorStyle DiscriminatorStyle { get; set; } // default: Property (JSON-like). Tag / Property / Both
    public string TypeDiscriminatorPropertyName { get; set; } // default: "$type"
    public YamlUnknownDerivedTypeHandling UnknownDerivedTypeHandling { get; set; } // Fail / FallBackToBase
}
```

### 6.3 Attributes

```csharp
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Interface)]
public sealed class YamlPolymorphicAttribute : Attribute
{
    public string? TypeDiscriminatorPropertyName { get; set; }
    public YamlTypeDiscriminatorStyle? DiscriminatorStyle { get; set; }
}

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Interface, AllowMultiple = true)]
public sealed class YamlDerivedTypeAttribute : Attribute
{
    public YamlDerivedTypeAttribute(Type derivedType, string discriminator);
    public string? Tag { get; set; } // optional explicit YAML tag (e.g. "!dog")
}
```

Mapping from JSON attributes:

- `[JsonPolymorphic(TypeDiscriminatorPropertyName = "kind")]` maps to `YamlPolymorphic` property-discriminator.
- `[JsonDerivedType(typeof(Dog), "dog")]` maps to a derived type with discriminator `"dog"`.

Serialization rules:

- When writing:
  - If `DiscriminatorStyle` includes Tag and a tag is available, emit tag.
  - If `DiscriminatorStyle` includes Property, emit discriminator member first (ordering must be stable).
- When reading:
  - If `Both`, accept tag or property discriminator.
  - If both are present and disagree, throw `YamlException`.

## 7. Serialization Semantics

### 7.1 Scalars (schema-sensitive)

Default schema: YAML 1.2 Core. Ensure:

- Plain scalars `true`/`false` map to `bool`.
- Plain `null`/`~` map to null.
- Integers support underscores and base prefixes (`0x`, `0o`).
- Floats support underscores, exponent, `.inf`, `.nan`.

Quoted scalars must always deserialize as strings (unless an explicit tag indicates otherwise).

### 7.2 Objects / mappings

Rules:

- Only serialize members that are:
  - Public get/set properties by default.
  - Public fields only if `IncludeFields`-like option is introduced (optional).
  - Non-public members only when `[YamlInclude]` / `[JsonInclude]` is used.
- Respect rename and ignore attributes.
- Ordering (default: preserve declaration order, with a sorted mode for diffability):
  - If an order attribute is present (`YamlPropertyOrder` / `JsonPropertyOrder`), use it as the primary key.
  - If `YamlSerializerOptions.MappingOrder == Declaration` (default), preserve declaration order for members with equal order.
    - Reflection resolver must approximate declaration order by sorting by `MemberInfo.MetadataToken` when available, and otherwise fall back to stable name ordering.
  - If `YamlSerializerOptions.MappingOrder == Sorted`, sort mapping members by their final emitted name (after rename/naming policy) using ordinal comparison.

### 7.3 Collections

Support the common collection set (reflection + source-gen):

- `T[]`, `List<T>`, `IList<T>`, `IReadOnlyList<T>`, `ICollection<T>`, `IEnumerable<T>`.
- `Dictionary<string, TValue>`, `IDictionary<string, TValue>`, `IReadOnlyDictionary<string, TValue>`.
- Non-string dictionary keys:
  - Supported when keys are scalars that can be emitted as YAML plain scalars.
  - Controlled by `DictionaryKeyPolicy` and a “key converter” policy.

### 7.4 Enums

Provide enum emission options:

- Default: emit enum names (string).
- Optional: emit numeric value.

If `[EnumMember(Value = "...")]` exists, treat it as the emitted name (optional v3.0 feature; implement if demanded by tests).

### 7.5 Anchors / aliases (reference preservation)

Define:

- `YamlReferenceHandling.None` (default): no alias emission; repeated references become repeated values.
- `YamlReferenceHandling.Preserve`: emit anchors/aliases to preserve object reference graphs.

The preserve mode must:

- Detect cycles and preserve correctly.
- Use stable anchor naming (`id001`, `id002`, ...).

### 7.6 Extension data

Support `JsonExtensionData` and `YamlExtensionData`:

- Target types:
  - `IDictionary<string, object?>`
  - `IDictionary<string, YamlNode>` (if a DOM model remains)
  - `IDictionary<string, YamlAny>` (if a new “any” representation is introduced)
- Unknown mapping keys are stored there during deserialization (when enabled).
- When serializing, extension data members are written after normal members (or before, but deterministic).

## 8. Reader/Writer Foundations (Span<char>)

### 8.1 Text-first APIs

YAML is primarily used for configuration and human-authored documents. In v3 the primary IO surface is UTF-16 text (`string`, `TextReader`, `TextWriter`).

### 8.2 Span-based implementation (allocation control)

Internals should be structured to minimize allocations by keeping data in spans as long as possible:

- Scanner/parser should operate on `ReadOnlySpan<char>` windows (with a `TextReader` adapter for streaming).
- Scalars should stay as spans until a type converter requires materialization.
- Use pooling for temporary buffers (`ArrayPool<char>`) where unavoidable.

Introduce internal abstractions to keep this swap-friendly:

- `IYamlInput` returning `ReadOnlySpan<char>` windows (and a `TextReader`-backed implementation).
- `IYamlOutput` writing to `TextWriter`.

### 8.3 Lossless Syntax Layer (Perfect Roundtrip + Source Spans)

In addition to `YamlSerializer` (object mapping), v3 must provide a low-level YAML API intended for editors and tooling.

Requirements:

- **Perfect roundtrip** for unmodified documents: parsing + emitting must preserve the original text exactly (including comments, whitespace, quoting style, and line endings).
- **Accurate source spans** for syntax highlighting and diagnostics: tokens and nodes must expose absolute offsets and line/column information.
- Object mapping (`YamlSerializer`) is explicitly **not** required to preserve formatting/comments and therefore does **not** roundtrip.

Proposed API shape (subject to small naming adjustments, but the capabilities are required):

```csharp
namespace SharpYaml.Syntax;

public sealed class YamlSyntaxTree
{
    public static YamlSyntaxTree Parse(string yaml, YamlSyntaxOptions? options = null);

    public string Text { get; } // original text
    public YamlSyntaxNode Root { get; }
    public IReadOnlyList<YamlSyntaxToken> Tokens { get; } // includes trivia when enabled

    // Perfect roundtrip for the parsed text (when unmodified).
    public string ToFullString();
    public void WriteTo(TextWriter writer);
}

public sealed class YamlSyntaxOptions
{
    public bool IncludeTrivia { get; set; } // default: true (comments/whitespace)
}

public abstract class YamlSyntaxNode
{
    public YamlSourceSpan Span { get; } // span of the syntactic construct (no trivia)
    public YamlSourceSpan FullSpan { get; } // span including trivia (when available)
}

public readonly struct YamlSyntaxToken
{
    public YamlSyntaxKind Kind { get; }
    public YamlSourceSpan Span { get; }
}

public readonly struct YamlSourceSpan
{
    public Mark Start { get; }
    public Mark End { get; }
}

public enum YamlSyntaxKind
{
    // Includes at minimum: CommentTrivia, WhitespaceTrivia, NewLineTrivia,
    // Scalar, Indicators/Punctuation needed for highlighting, Directives, Tags, Anchors, etc.
}
```

Behavioral requirements:

- `YamlSyntaxTree.Parse(text).ToFullString()` must equal `text` exactly for valid YAML input.
- All spans must be consistent with the source text:
  - `Mark.Index` is 0-based absolute offset in `Text`.
  - `Mark.Line` and `Mark.Column` are 0-based.
- Parse errors must surface `YamlException` with correct `Start`/`End` marks for the offending region.

## 9. NativeAOT / Trimming Requirements

### 9.1 Runtime behavior

- The source-generated path must avoid reflection at runtime.
- Reflection-based serialization must:
  - Be clearly marked as unsafe for trimming (via `[RequiresUnreferencedCode]`) unless fully annotated.
  - Be gated by `YamlSerializer.IsReflectionEnabledByDefault` (default: true), which can be disabled via `AppContext.SetSwitch("SharpYaml.YamlSerializer.IsReflectionEnabledByDefault", false)` before first use.
  - When reflection is disabled and no generated metadata is available, `YamlSerializer` overloads that do not accept `YamlTypeInfo` must throw an `InvalidOperationException` explaining how to:
    - Provide generated metadata (use `YamlSerializer` overloads that accept a `YamlSerializerContext`, or pass `<context>.Options` to an `options`-based overload), or
    - Re-enable reflection (app context switch).

### 9.2 Generator deliverables

The incremental generator must:

- Generate `YamlTypeInfo<T>` graphs for all `[JsonSerializable]` entries declared on `YamlSerializerContext`-derived classes.
- Generate member accessors without reflection (delegates or direct code).
- Encode attribute-derived metadata (names, ignore, requiredness, ordering, polymorphism).
- Generate converter registration and collection handlers for discovered generic instantiations.

### 9.3 Trimming-friendly design

- Avoid `Type.GetType(string)`-style tag-to-type resolution by default.
- If tag-to-type is supported, it must be behind an explicit opt-in, and ideally driven by generated metadata.

## 10. Test Strategy (MSTest + Robustness)

### 10.1 Migrate test framework

- Replace NUnit with MSTest in `src/SharpYaml.Tests/`.
- Use MSTest APIs directly (`Assert.AreEqual`, `Assert.IsTrue`, `Assert.ThrowsException`, etc.).
- Do not introduce any NUnit compatibility layer/wrapper, adapter assertions, or fluent facade.
- Use:
  - `MSTest.TestFramework`
  - `MSTest.TestAdapter`
  - `Microsoft.NET.Test.Sdk`

### 10.2 Test categories (must exist)

1. **Syntax tree roundtrip + spans**
   - Roundtrip: `YamlSyntaxTree.Parse(text).ToFullString() == text` for representative YAML inputs including:
     - Comments and blank lines.
     - Flow vs block collections.
     - Quoted scalars and escape sequences.
     - Anchors/tags/directives.
     - Different line endings (LF/CRLF).
   - Spans: validate `Mark.Index`/`Line`/`Column` for key tokens and nodes (syntax highlighting correctness).
   - Diagnostics: invalid YAML cases must throw `YamlException` with correct `Start`/`End`.

2. **Parser/Scanner/Emitter unit tests**
   - Keep coverage for low-level tokenization and events.
   - Add regression tests for YAML 1.2 behaviors that currently differ from spec.

3. **Serializer behavioral tests**
   - Member naming (Yaml + Json attributes, naming policies).
   - Null/default handling.
   - Collections and dictionaries.
   - Polymorphism (tag-based and property-based).
   - Reference preservation (anchors/aliases).

4. **Source generator integration tests**
   - A test project that uses `[JsonSerializable]` on a `YamlSerializerContext` + generated context.
   - Validates that:
     - The generated code compiles.
     - Serialization works without reflection.
   - Prefer “compile + run” tests rather than snapshotting generated text.

5. **NativeAOT smoke test**
   - A small console app under `src/` or `samples/` published with `PublishAot=true`.
   - The test validates:
     - It builds.
     - It can serialize + deserialize a sample model using the generated context.

### 10.3 Golden files and diffable assertions

Use golden YAML files for emitted output where readability matters:

- Store under `src/SharpYaml.Tests/files/v3/`.
- Normalize line endings and indentation.
- Provide a helper to compare with clear diffs (show first difference and context).

## 11. Implementation Plan (Concrete Steps)

This is the recommended execution order. Each step should be a self-contained PR/commit with tests.

1. **Repo + build modernization**
   - Update `src/global.json` to .NET 10 SDK.
   - Update `SharpYaml.csproj` to `net10.0;net8.0;netstandard2.0`, `Nullable=enable`, latest LangVersion.
   - Update `SharpYaml.Tests.csproj` to `net10.0` only.
   - Remove `Polyfills/` where no longer needed.

2. **Lossless syntax layer (roundtrip + spans)**
   - Add a public `SharpYaml.Syntax` API (`YamlSyntaxTree`, tokens, `YamlSourceSpan`) per section 8.3.
   - Update scanner/parser infrastructure to preserve trivia (comments/whitespace) when requested.
   - Ensure parse errors report precise spans for editor scenarios.
   - Add roundtrip + span tests (section 10.2).

3. **Introduce new public API surface**
   - Add `YamlSerializer`, `YamlSerializerOptions`, naming policy types, enums for schema and handling knobs.
   - Remove legacy serializer APIs and update `README.md` / migration docs to point to the v3 APIs.

4. **Metadata-first serialization pipeline**
   - Implement the `YamlTypeInfo` model and `IYamlTypeInfoResolver`.
   - Implement a reflection-based resolver (guarded by trimming annotations and options).
   - Implement a converter pipeline for scalars, collections, objects, dictionaries.
   - Implement attribute reading for YAML + JSON attributes (section 5).

5. **Polymorphism**
   - Implement `YamlPolymorphismOptions`, derived type registries.
   - Support tag and property discriminator styles.
   - Add extensive tests.

6. **Source generator**
   - Add new analyzer project `src/SharpYaml.SourceGeneration/`.
   - Implement incremental generator that targets `YamlSerializerContext`-derived classes and reads `[JsonSerializable(typeof(...))]` entries to determine the type graph to generate.
   - Ensure generated metadata supports the v3 serializer pipeline without reflection.

7. **Span<char> and streaming**
   - Add `Deserialize(ReadOnlySpan<char>)` overloads.
   - Add `TextReader` overloads and ensure line/column tracking stays correct for streaming input.
   - Add perf-oriented tests around allocation counts for common config scenarios (optional).

8. **YAML 1.2 compatibility improvements**
   - Add missing scalar parsing cases covered by YAML 1.2 tests.
   - Improve duplicate key handling behavior per `YamlDuplicateKeyHandling`.
   - Expand schema tests, potentially using a curated subset of the YAML test suite.

9. **Test suite migration and hardening**
   - Complete NUnit -> MSTest conversion.
   - Ensure tests use direct MSTest assertions only (no NUnit wrapper/facade).
   - Add generator and NativeAOT integration tests.
   - Add golden file coverage.

10. **Docs and migration guide**
   - Add `site/migration.md`.
   - Update `README.md` usage examples to the new API.

## 12. Open Questions (Must Be Answered Before Coding)

- None currently. Add items here only when the answer materially changes public API or default behaviors.
