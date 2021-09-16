# Changelog

## 1.8.0 (16 Sep 2021)
- Add `SerializerSettings.RespectPrivateSetters` (PR https://github.com/xoofx/SharpYaml/pull/61)
- Fix clearing of mapping/sequence, and update IsImplicit when setting Tag (PR https://github.com/xoofx/SharpYaml/pull/78)
- Always enclose keys in quotes when emitting JSON (PR https://github.com/xoofx/SharpYaml/pull/82)
- Optimize memory usage (PR https://github.com/xoofx/SharpYaml/pull/87)
- Fix EmitJsonComptible typo by hiding old member and adding fixed one (PR https://github.com/xoofx/SharpYaml/pull/86)
- When an item is inserted/removed from a container, update the indices of the subsequent children in the tracker (PR https://github.com/xoofx/SharpYaml/pull/84)
- Allow to Ignore unmapped properties in YAML `SerializerSettings.IgnoreUnmatchedProperties` (PR https://github.com/xoofx/SharpYaml/pull/91)
- Remove dependency on System.Reflection.TypeExtensions from netstandad2.0 (PR https://github.com/xoofx/SharpYaml/pull/92)

## 1.7.0 (8 Mar 2020)
- Add PascalCase and CamelCase naming convention support

## 1.6.6
- Handle parsing unicode surrogate pairs.
- Fix serialization with DefaultStyle set to Flow
- Make CoreSchema a singleton so that PrepareScalarRules only runs once.

## 1.6.5
- Support for escaping slash for JSON Compatibility (PR https://github.com/xoofx/SharpYaml/pull/66)

## 1.6.4
- .NETStandart 2.0 supports
- Fix ChildIndex.Resolve avoids OutOfRangeExceptions, returns null instead (#57)
- Enable SourceLink (#58)
- Fix tracker on YamlDocument (#53)

## 1.6.3
- .NETStandart 1.3 supports
- Add better high-precision double to round-trip 
- Expose new signature for YamlNode.ToObject which takes a Type reference.
- Add support for events and subscribers to be notified of model changes.

## 1.6.2
- Add YamlNode hierarchy to allow modification of the parse tree.
- Add support for Guid and DateTimeOffset

## 1.6.1
- Improve support for unicode escape characters

## 1.6.0      
- Add support for CoreCLR (netstandard1.6+)