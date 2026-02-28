using System;
using System.IO;
using System.Runtime.CompilerServices;
using SharpYaml.Serialization;
using SharpYaml.Serialization.Converters;

namespace SharpYaml;

/// <summary>
/// Provides reflection-based type metadata for <see cref="YamlSerializer"/>.
/// </summary>
public sealed class ReflectionYamlTypeInfoResolver : IYamlTypeInfoResolver
{
    private sealed class ReflectionYamlTypeInfo : YamlTypeInfo
    {
        private readonly YamlConverter _converter;

        public ReflectionYamlTypeInfo(Type type, YamlSerializerOptions options, YamlConverter converter) : base(type, options)
        {
            _converter = converter;
        }

        public override string SerializeAsString(object? value)
        {
            using var writer = new StringWriter(System.Globalization.CultureInfo.InvariantCulture);
            var yamlWriter = new YamlWriter(writer, Options);
            _converter.Write(yamlWriter, value, Options);
            var text = writer.ToString();
            return text.EndsWith("\n", StringComparison.Ordinal) ? text : text + "\n";
        }

        public override object? DeserializeFromString(string yaml)
        {
            ArgumentNullException.ThrowIfNull(yaml);
            var reader = YamlReader.Create(yaml, Options);
            if (!reader.Read())
            {
                return null;
            }

            return _converter.Read(ref reader, Type, Options);
        }
    }

    private readonly ConditionalWeakTable<YamlSerializerOptions, YamlBuiltInConverterResolver> _converterResolvers = new();

    /// <summary>
    /// Gets a shared default reflection resolver instance.
    /// </summary>
    public static ReflectionYamlTypeInfoResolver Default { get; } = new();

    /// <inheritdoc />
    public YamlTypeInfo? GetTypeInfo(Type type, YamlSerializerOptions options)
    {
        ArgumentNullException.ThrowIfNull(type);
        ArgumentNullException.ThrowIfNull(options);
        var resolver = _converterResolvers.GetValue(options, static o => new YamlBuiltInConverterResolver(o));
        var converter = resolver.GetConverter(type);
        return new ReflectionYamlTypeInfo(type, options, converter);
    }
}
