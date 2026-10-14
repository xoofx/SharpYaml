// // Copyright (c) Alexandre Mutel. All rights reserved.
// // Licensed under the MIT license.
// // See LICENSE.txt file in the project root for full license information.

using System;

namespace SharpYaml.Serialization.Converters;

internal sealed class YamlNullableConverter<T> : YamlConverter<T?> where T : struct
{
    public static YamlNullableConverter<T> Instance { get; } = new();

    public override T? Read(YamlReader reader)
    {
        if (reader.TokenType == YamlTokenType.Scalar && YamlScalar.IsNull(reader))
        {
            reader.Read();
            return null;
        }

        return (T?)reader.GetConverter(typeof(T)).Read(reader, typeof(T));
    }

    public override void Write(YamlWriter writer, T? value)
    {
        if (!value.HasValue)
        {
            writer.WriteNullValue();
            return;
        }

        writer.GetConverter(typeof(T)).Write(writer, value.GetValueOrDefault());
    }
}

// Adapt an options converter without constructing a closed generic type at runtime.
internal sealed class YamlNullableCustomConverter(Type nullableType, Type underlyingType, YamlConverter converter) : YamlConverter
{
    public override bool CanConvert(Type typeToConvert) => typeToConvert == nullableType;

    public override object? Read(YamlReader reader, Type typeToConvert)
    {
        if (reader.TokenType == YamlTokenType.Scalar && YamlScalar.IsNull(reader))
        {
            reader.Read();
            return null;
        }

        return converter.Read(reader, underlyingType);
    }

    public override void Write(YamlWriter writer, object? value)
    {
        if (value is null)
        {
            writer.WriteNullValue();
            return;
        }

        converter.Write(writer, value);
    }
}
