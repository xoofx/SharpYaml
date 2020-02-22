# Changelog

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