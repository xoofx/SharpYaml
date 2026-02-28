using System;

namespace SharpYaml.Serialization;

/// <summary>
/// Indicates which constructor should be used for YAML deserialization.
/// </summary>
[AttributeUsage(AttributeTargets.Constructor)]
public sealed class YamlConstructorAttribute : Attribute
{
}

