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

        return ((YamlConverter<T>)reader.GetConverter(typeof(T))).Read(reader);
    }

    public override void Write(YamlWriter writer, T? value)
    {
        if (!value.HasValue)
        {
            writer.WriteNullValue();
            return;
        }

        ((YamlConverter<T>)writer.GetConverter(typeof(T))).Write(writer, value.GetValueOrDefault());
    }
}
