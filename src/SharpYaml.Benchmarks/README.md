# SharpYaml Benchmarks

This project benchmarks SharpYaml against the latest stable YamlDotNet packages:

- `YamlDotNet` `16.3.0`
- `Vecc.YamlDotNet.Analyzers.StaticGenerator` `16.3.0`

## Scenarios

- Generic-structure serialization of a large document (`Dictionary<string, object?>`).
- Large `List<string>` serialization.
- POCO serialization and deserialization.
- Source-generated/static-context serialization:
  - SharpYaml incremental source generator via `YamlSerializerContext`.
  - YamlDotNet static generator via `YamlStaticContext` + `YamlSerializable`.

## Run

From `src/`:

```bash
dotnet run -c Release --project SharpYaml.Benchmarks -- --filter "*"
```

Optional: run only source-generated benchmarks:

```bash
dotnet run -c Release --project SharpYaml.Benchmarks -- --filter "*SourceGenerated*"
```

## Output

BenchmarkDotNet is configured with:

- `ConfigOptions.JoinSummary` so all benchmarks are reported in a single combined summary table at the end of the run.
- `BenchmarkLogicalGroupRule.ByCategory` so ratio/baseline comparisons are scoped to each benchmark scenario (e.g. POCO serialize vs POCO deserialize).
