# YAML Test Suite fixture

`yaml-test-suite.json` is derived from [yaml/yaml-test-suite](https://github.com/yaml/yaml-test-suite)
at commit `da267a5c4782e7361e82889e76c0dc7df0e1e870` (the source branch, not the older generated data release).
The upstream MIT license is reproduced in `yaml-test-suite.LICENSE.txt`.

The fixture contains all 402 enabled cases: 308 valid streams and 94 syntax errors.
Input characters and line endings are encoded as JSON escapes so Git's newline conversion cannot change them.
`YamlTestSuiteTests` compares exact parsing events through both input buffers, then loads valid cases and
compares their first document to upstream JSON when available. Loading uses `LastWins`, with both default
alias expansion and `Preserve`: duplicate-key policy is separate from parsing syntax. The parser checks every document.
Complex keys have no JSON counterpart; their shape and round trips have dedicated tests.

Upstream marks `ZYU8` with `skip: true`, and its own data generator excludes it. We follow that exclusion.
In particular, the skipped `%YAML 1.1 1.2` case conflicts with enabled `H7TQ`, which rejects extra directive
parameters. The benchmark includes these skipped cases, so its score is not the enabled upstream-suite score.
Reserved directive names and future minor versions from the other skipped examples have separate regressions.

To regenerate, check out the pinned upstream revision and run
`python tools/import-yaml-test-suite.py <upstream-checkout>` from the SharpYaml root.
Regeneration requires PyYAML; building and running the .NET tests does not.
