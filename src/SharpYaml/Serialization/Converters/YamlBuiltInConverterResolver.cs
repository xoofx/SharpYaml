using System;
using System.Collections.Generic;

namespace SharpYaml.Serialization.Converters;

internal sealed class YamlBuiltInConverterResolver : IYamlConverterResolver
{
    private readonly YamlSerializerOptions _options;
    private readonly Dictionary<Type, YamlConverter> _cache = new();

    public YamlBuiltInConverterResolver(YamlSerializerOptions options)
    {
        _options = options;
    }

    public YamlConverter GetConverter(Type typeToConvert)
    {
        if (_cache.TryGetValue(typeToConvert, out var cached))
        {
            return cached;
        }

        var converter = CreateConverter(typeToConvert);
        _cache[typeToConvert] = converter;
        return converter;
    }

    private YamlConverter CreateConverter(Type typeToConvert)
    {
        if (_options.TryGetCustomConverter(typeToConvert, out var custom))
        {
            return custom;
        }

        if (typeToConvert == typeof(string))
        {
            return YamlStringConverter.Instance;
        }

        if (typeToConvert == typeof(bool))
        {
            return YamlBooleanConverter.Instance;
        }

        if (typeToConvert == typeof(byte))
        {
            return YamlByteConverter.Instance;
        }

        if (typeToConvert == typeof(sbyte))
        {
            return YamlSByteConverter.Instance;
        }

        if (typeToConvert == typeof(short))
        {
            return YamlInt16Converter.Instance;
        }

        if (typeToConvert == typeof(ushort))
        {
            return YamlUInt16Converter.Instance;
        }

        if (typeToConvert == typeof(int))
        {
            return YamlInt32Converter.Instance;
        }

        if (typeToConvert == typeof(uint))
        {
            return YamlUInt32Converter.Instance;
        }

        if (typeToConvert == typeof(long))
        {
            return YamlInt64Converter.Instance;
        }

        if (typeToConvert == typeof(ulong))
        {
            return YamlUInt64Converter.Instance;
        }

        if (typeToConvert == typeof(double))
        {
            return YamlDoubleConverter.Instance;
        }

        if (typeToConvert == typeof(float))
        {
            return YamlSingleConverter.Instance;
        }

        if (typeToConvert == typeof(decimal))
        {
            return YamlDecimalConverter.Instance;
        }

        if (typeToConvert == typeof(char))
        {
            return YamlCharConverter.Instance;
        }

        if (typeToConvert == typeof(nint))
        {
            return YamlIntPtrConverter.Instance;
        }

        if (typeToConvert == typeof(nuint))
        {
            return YamlUIntPtrConverter.Instance;
        }

        if (typeToConvert == typeof(object))
        {
            return new YamlUntypedObjectConverter(this);
        }

        var underlyingNullable = Nullable.GetUnderlyingType(typeToConvert);
        if (underlyingNullable is not null)
        {
            var inner = GetConverter(underlyingNullable);
            var converterType = typeof(YamlNullableConverter<>).MakeGenericType(underlyingNullable);
            return (YamlConverter)Activator.CreateInstance(converterType, inner)!;
        }

        if (typeToConvert.IsEnum)
        {
            var converterType = typeof(YamlEnumConverter<>).MakeGenericType(typeToConvert);
            return (YamlConverter)Activator.CreateInstance(converterType)!;
        }

        if (typeToConvert.IsArray)
        {
            var elementType = typeToConvert.GetElementType()!;
            var converterType = typeof(YamlArrayConverter<>).MakeGenericType(elementType);
            return (YamlConverter)Activator.CreateInstance(converterType, this)!;
        }

        if (typeToConvert.IsGenericType)
        {
            var definition = typeToConvert.GetGenericTypeDefinition();
            var args = typeToConvert.GetGenericArguments();

            if (definition == typeof(List<>))
            {
                var converterType = typeof(YamlListConverter<>).MakeGenericType(args[0]);
                return (YamlConverter)Activator.CreateInstance(converterType, this)!;
            }

            if (definition == typeof(Dictionary<,>) && args[0] == typeof(string))
            {
                var converterType = typeof(YamlDictionaryConverter<>).MakeGenericType(args[1]);
                return (YamlConverter)Activator.CreateInstance(converterType, this)!;
            }

            if (definition == typeof(IEnumerable<>))
            {
                var converterType = typeof(YamlEnumerableConverter<>).MakeGenericType(args[0]);
                return (YamlConverter)Activator.CreateInstance(converterType, this)!;
            }
        }

        var objectConverterType = typeof(YamlObjectConverter<>).MakeGenericType(typeToConvert);
        return (YamlConverter)Activator.CreateInstance(objectConverterType, this)!;
    }
}
