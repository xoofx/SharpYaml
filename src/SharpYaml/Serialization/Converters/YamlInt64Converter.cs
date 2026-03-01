// // Copyright (c) Alexandre Mutel. All rights reserved.
// // Licensed under the MIT license.
// // See LICENSE.txt file in the project root for full license information.

using System;

namespace SharpYaml.Serialization.Converters;

internal sealed class YamlInt64Converter : YamlConverter<long>
{
    public static YamlInt64Converter Instance { get; } = new();

    public override long Read(YamlReader reader)
    {
        if (reader.TokenType != YamlTokenType.Scalar)
        {
            throw YamlThrowHelper.ThrowExpectedScalar(reader);
        }

        var value = reader.ScalarValue.AsSpan();
        if (!YamlScalar.TryParseInt64(value, out var result))
        {
            throw YamlThrowHelper.ThrowInvalidIntegerScalar(reader);
        }

        reader.Read();
        return result;
    }

    public override void Write(YamlWriter writer, long value)
    {
        writer.WriteScalar(value.ToString(System.Globalization.CultureInfo.InvariantCulture));
    }
}
