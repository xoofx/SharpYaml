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
    public abstract object? Read(ref YamlReader reader, Type typeToConvert, YamlSerializerOptions options);

    /// <summary>
    /// Writes a value to YAML.
    /// </summary>
    public abstract void Write(YamlWriter writer, object? value, YamlSerializerOptions options);
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
    public sealed override object? Read(ref YamlReader reader, Type typeToConvert, YamlSerializerOptions options)
    {
        return Read(ref reader, options);
    }

    /// <inheritdoc />
    public sealed override void Write(YamlWriter writer, object? value, YamlSerializerOptions options)
    {
        Write(writer, (T)value!, options);
    }

    /// <summary>
    /// Reads a value from YAML.
    /// </summary>
    public abstract T? Read(ref YamlReader reader, YamlSerializerOptions options);

    /// <summary>
    /// Writes a value to YAML.
    /// </summary>
    public abstract void Write(YamlWriter writer, T value, YamlSerializerOptions options);
}

