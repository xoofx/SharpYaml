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
    /// Reads a value from YAML.
    /// </summary>
    public abstract object? Read(YamlReader reader, Type typeToConvert);

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

