# SharpYaml

**SharpYaml** is a .NET library that provides a **YAML parser and serialization engine** for .NET objects.

## Usage

```C#
var serializer = new Serializer();
var text = serializer.Serialize(new { List = new List<int>() { 1, 2, 3 }, Name = "Hello", Value = "World!" });
Console.WriteLine(text);
```   
Output:

	List:
	  - 1
	  - 2
	  - 3
	Name: Hello
	Value: World!

## Features

SharpYaml is a fork of [YamlDotNet](http://www.aaubry.net/yamldotnet.aspx) and is adding the following features:

 - Supports for 4.5+ .NET Portable Class Library compatible with Windows desktop, Windows Phone 8 and Windows RT
 - Completely rewritten serialization/deserialization engine
 - A single interface `IYamlSerializable` for implementing custom serializers, along `IYamlSerializableFactory` to allow dynamic creation of serializers. Registration can be done through `SerializerSettings.RegisterSerializer` and `SerializerSettings.RegisterSerializerFactory`
   - Can inherit from `ScalarSerializerBase` to provide custom serialization to/from a Yaml scalar 
 - Supports for custom collection that contains user properties to serialize along the collection.
 - Supports for Yaml 1.2 schemas 
 - A centralized type system through `ITypeDescriptor` and `IMemberDescriptor`
 - Highly configurable serialization using `SerializerSettings` (see usage)
   - Add supports to register custom attributes on external objects (objects that you can't modify) by using `SerializerSettings.Register(memberInfo, attribute)`
   - Several options and settings: `EmitAlias`, `IndentLess`, `SortKeyForMapping`, `EmitJsonComptible`, `EmitCapacityForList`, `LimitPrimitiveFlowSequence`, `EmitDefaultValues`
   - Add supports for overriding the Yaml style of serialization (block or flow) with `SerializerSettings.DefaultStyle` and `SerializerSettings.DynamicStyleFormat`  
 - Supports for registering an assembly when discovering types to deserialize through `SerializerSettings.RegisterAssembly`
 - Memory allocation and GC pressure improved

## Available from Nuget 
You can download **SharpYaml** binaries directly from [nuget](http://www.nuget.org/packages?q=sharpyaml).

## License
MIT