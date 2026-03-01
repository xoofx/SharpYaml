using System;
using System.Collections.Generic;
using SharpYaml.Model;
using SharpYaml.Serialization.Converters;

namespace SharpYaml.Serialization;

internal static class YamlBuiltInTypeInfoResolver
{
    public static YamlTypeInfo? GetTypeInfo(Type type, YamlSerializerOptions options)
    {
        ArgumentNullException.ThrowIfNull(type);
        ArgumentNullException.ThrowIfNull(options);

        var converter = GetConverter(type);
        return converter is null ? null : new BuiltInYamlTypeInfo(type, options, converter);
    }

    private static YamlConverter? GetConverter(Type type)
    {
        if (type == typeof(string))
        {
            return YamlStringConverter.Instance;
        }

        if (type == typeof(bool))
        {
            return YamlBooleanConverter.Instance;
        }

        if (type == typeof(byte))
        {
            return YamlByteConverter.Instance;
        }

        if (type == typeof(sbyte))
        {
            return YamlSByteConverter.Instance;
        }

        if (type == typeof(short))
        {
            return YamlInt16Converter.Instance;
        }

        if (type == typeof(ushort))
        {
            return YamlUInt16Converter.Instance;
        }

        if (type == typeof(int))
        {
            return YamlInt32Converter.Instance;
        }

        if (type == typeof(uint))
        {
            return YamlUInt32Converter.Instance;
        }

        if (type == typeof(long))
        {
            return YamlInt64Converter.Instance;
        }

        if (type == typeof(ulong))
        {
            return YamlUInt64Converter.Instance;
        }

        if (type == typeof(nint))
        {
            return YamlIntPtrConverter.Instance;
        }

        if (type == typeof(nuint))
        {
            return YamlUIntPtrConverter.Instance;
        }

        if (type == typeof(float))
        {
            return YamlSingleConverter.Instance;
        }

        if (type == typeof(double))
        {
            return YamlDoubleConverter.Instance;
        }

        if (type == typeof(decimal))
        {
            return YamlDecimalConverter.Instance;
        }

        if (type == typeof(char))
        {
            return YamlCharConverter.Instance;
        }

        if (type == typeof(object))
        {
            return YamlUntypedObjectConverter.Instance;
        }

        if (type == typeof(Dictionary<string, object?>) || type == typeof(Dictionary<string, object>))
        {
            return YamlDictionaryObjectConverter.Instance;
        }

        if (type == typeof(List<object?>) || type == typeof(List<object>))
        {
            return YamlListObjectConverter.Instance;
        }

        if (type == typeof(object[]))
        {
            return YamlObjectArrayConverter.Instance;
        }

        if (typeof(YamlNode).IsAssignableFrom(type))
        {
            return YamlModelNodeConverter.Instance;
        }

        if (type == typeof(bool?)) return YamlNullableConverter<bool>.Instance;
        if (type == typeof(byte?)) return YamlNullableConverter<byte>.Instance;
        if (type == typeof(sbyte?)) return YamlNullableConverter<sbyte>.Instance;
        if (type == typeof(short?)) return YamlNullableConverter<short>.Instance;
        if (type == typeof(ushort?)) return YamlNullableConverter<ushort>.Instance;
        if (type == typeof(int?)) return YamlNullableConverter<int>.Instance;
        if (type == typeof(uint?)) return YamlNullableConverter<uint>.Instance;
        if (type == typeof(long?)) return YamlNullableConverter<long>.Instance;
        if (type == typeof(ulong?)) return YamlNullableConverter<ulong>.Instance;
        if (type == typeof(nint?)) return YamlNullableConverter<nint>.Instance;
        if (type == typeof(nuint?)) return YamlNullableConverter<nuint>.Instance;
        if (type == typeof(float?)) return YamlNullableConverter<float>.Instance;
        if (type == typeof(double?)) return YamlNullableConverter<double>.Instance;
        if (type == typeof(decimal?)) return YamlNullableConverter<decimal>.Instance;
        if (type == typeof(char?)) return YamlNullableConverter<char>.Instance;

        return null;
    }

    private sealed class BuiltInYamlTypeInfo : YamlTypeInfo
    {
        private readonly YamlConverter _converter;

        public BuiltInYamlTypeInfo(Type type, YamlSerializerOptions options, YamlConverter converter)
            : base(type, options)
        {
            _converter = converter ?? throw new ArgumentNullException(nameof(converter));
        }

        public override void Write(YamlWriter writer, object? value)
        {
            ArgumentNullException.ThrowIfNull(writer);
            _converter.Write(writer, value);
        }

        public override object? ReadAsObject(YamlReader reader)
        {
            ArgumentNullException.ThrowIfNull(reader);
            return _converter.Read(reader, Type);
        }
    }
}
