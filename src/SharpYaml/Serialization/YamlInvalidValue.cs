// // Copyright (c) Alexandre Mutel. All rights reserved.
// // Licensed under the MIT license.
// // See LICENSE.txt file in the project root for full license information.

using System;
using SharpYaml.Model;

namespace SharpYaml.Serialization;

/// <summary>
/// Describes a YAML value that a converter rejected while deserializing a member or a constructor argument, and that
/// was skipped because <see cref="YamlSerializerOptions.InvalidValueHandling"/> is not <see cref="YamlInvalidValueHandling.FailFast"/>.
/// </summary>
/// <remarks>
/// Instances are passed to <see cref="YamlSerializerOptions.InvalidValueCallback"/> and
/// <see cref="YamlSerializerOptions.InvalidValuesFinalizer"/>, and listed by <see cref="YamlInvalidValuesException"/>.
/// </remarks>
public sealed class YamlInvalidValue
{
    internal YamlInvalidValue(
        string? sourceName,
        Type declaringType,
        object? instance,
        string name,
        string path,
        Mark keyStart,
        Mark valueStart,
        Type targetType,
        YamlException exception)
    {
        SourceName = sourceName;
        DeclaringType = declaringType;
        Instance = instance;
        Name = name;
        Path = path;
        KeyStart = keyStart;
        ValueStart = valueStart;
        TargetType = targetType;
        Exception = exception;
    }

    /// <summary>Gets the source name (for example a file path) from <see cref="YamlSerializerOptions.SourceName"/>.</summary>
    public string? SourceName { get; }

    /// <summary>Gets the CLR type that was being deserialized when the value was rejected.</summary>
    public Type DeclaringType { get; }

    /// <summary>
    /// Gets the instance being populated, or <see langword="null"/> when it does not exist yet
    /// (for example for types deserialized through a parameterized constructor).
    /// </summary>
    public object? Instance { get; }

    /// <summary>Gets the YAML key of the member or constructor argument.</summary>
    public string Name { get; }

    /// <summary>
    /// Gets the path of the value from the document root, for example <c>$.servers[2].port</c>.
    /// Keys that are not simple identifiers are written as <c>$['a.b']</c>.
    /// </summary>
    /// <remarks>
    /// For values inside polymorphic nodes the path is complete, but <see cref="KeyStart"/> and
    /// <see cref="ValueStart"/> are relative to the buffered node rather than the document.
    /// </remarks>
    public string Path { get; }

    /// <summary>Gets the location of the key.</summary>
    public Mark KeyStart { get; }

    /// <summary>Gets the location where the value starts.</summary>
    public Mark ValueStart { get; }

    /// <summary>Gets the CLR type of the member or constructor parameter the value was meant for.</summary>
    public Type TargetType { get; }

    /// <summary>
    /// Gets the rejected value as a YAML element, or <see langword="null"/> when no callback or finalizer consumes it.
    /// </summary>
    public YamlElement? Value { get; private set; }

    /// <summary>
    /// Gets the reason the converter gave, without the location that <see cref="System.Exception.Message"/> prefixes.
    /// </summary>
    public string Message => Exception.Reason;

    /// <summary>Gets the exception the converter threw. Its <see cref="YamlException.Start"/> and <see cref="YamlException.End"/> locate the value.</summary>
    public YamlException Exception { get; }

    internal void SetValue(YamlElement? value) => Value = value;
}
