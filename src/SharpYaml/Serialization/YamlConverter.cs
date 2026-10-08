// // Copyright (c) Alexandre Mutel. All rights reserved.
// // Licensed under the MIT license.
// // See LICENSE.txt file in the project root for full license information.

using System;

namespace SharpYaml.Serialization;

/// <summary>
/// Converts between YAML tokens and a CLR type.
/// </summary>
public abstract class YamlConverter
{
    /// <summary>
    /// Determines whether this converter can handle <paramref name="typeToConvert"/>.
    /// </summary>
    public abstract bool CanConvert(Type typeToConvert);

    /// <summary>
    /// Determines whether this converter can handle <paramref name="typeToConvert"/>, additionally treating a
    /// converter that handles a non-nullable value type <c>T</c> as able to handle <see cref="Nullable{T}"/>
    /// of that same type.
    /// </summary>
    /// <remarks>
    /// <see cref="YamlConverter{T}"/>'s <see cref="CanConvert"/> override only ever matches <c>typeof(T)</c>
    /// exactly, so a converter registered for a value type <c>T</c> never matches a lookup for <c>T?</c>. This
    /// is safe to extend here because <see cref="YamlConverter{T}.Read(YamlReader, Type)"/> and
    /// <see cref="YamlConverter{T}.Write(YamlWriter, object?)"/> both ignore the requested type and operate on
    /// the boxed/unboxed <c>T</c> value directly - boxing a <c>T</c> and unboxing it as <c>T?</c> (and vice
    /// versa) is a supported CLR conversion. Converter resolution call sites that match candidate converters
    /// against a requested type should call this instead of <see cref="CanConvert"/> directly.
    /// </remarks>
    /// <param name="typeToConvert">The CLR type to resolve, used as-is or unwrapped from <see cref="Nullable{T}"/>.</param>
    /// <returns><see langword="true"/> when this converter can handle <paramref name="typeToConvert"/> or, when it is a <see cref="Nullable{T}"/>, its underlying type.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="typeToConvert"/> is <see langword="null"/>.</exception>
    public bool CanConvertNullable(Type typeToConvert)
    {
        ArgumentGuard.ThrowIfNull(typeToConvert);
        if (CanConvert(typeToConvert))
        {
            return true;
        }

        var underlyingType = Nullable.GetUnderlyingType(typeToConvert);
        return underlyingType is not null && CanConvert(underlyingType);
    }

    /// <summary>
    /// Reads a value from YAML.
    /// </summary>
    public abstract object? Read(YamlReader reader, Type typeToConvert);

    /// <summary>
    /// Determines whether this converter can populate an existing instance instead of creating a replacement.
    /// </summary>
    /// <remarks>
    /// The default implementation returns <see langword="false"/>.
    /// </remarks>
    public virtual bool CanPopulate(Type typeToConvert)
    {
        ArgumentGuard.ThrowIfNull(typeToConvert);
        return false;
    }

    /// <summary>
    /// Populates an existing value instance from YAML.
    /// </summary>
    /// <remarks>
    /// Converters that support population should override both <see cref="CanPopulate(Type)"/> and this method.
    /// The default implementation throws <see cref="NotSupportedException"/>.
    /// </remarks>
    public virtual object? Populate(YamlReader reader, Type typeToConvert, object existingValue)
    {
        ArgumentGuard.ThrowIfNull(reader);
        ArgumentGuard.ThrowIfNull(typeToConvert);
        ArgumentGuard.ThrowIfNull(existingValue);
        throw new NotSupportedException($"Converter '{GetType()}' does not support populating '{typeToConvert}'.");
    }

    /// <summary>
    /// Writes a value to YAML.
    /// </summary>
    public abstract void Write(YamlWriter writer, object? value);
}

/// <summary>
/// Converts between YAML and a specific CLR type.
/// </summary>
/// <typeparam name="T">The CLR type handled by this converter.</typeparam>
public abstract class YamlConverter<T> : YamlConverter
{
    /// <inheritdoc />
    public sealed override bool CanConvert(Type typeToConvert) => typeToConvert == typeof(T);

    /// <inheritdoc />
    public sealed override object? Read(YamlReader reader, Type typeToConvert)
    {
        return Read(reader);
    }

    /// <inheritdoc />
    public sealed override void Write(YamlWriter writer, object? value)
    {
        Write(writer, (T)value!);
    }

    /// <summary>
    /// Reads a value from YAML.
    /// </summary>
    public abstract T? Read(YamlReader reader);

    /// <summary>
    /// Writes a value to YAML.
    /// </summary>
    public abstract void Write(YamlWriter writer, T value);
}

