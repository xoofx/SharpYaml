// // Copyright (c) Alexandre Mutel. All rights reserved.
// // Licensed under the MIT license.
// // See LICENSE.txt file in the project root for full license information.

using System;

namespace SharpYaml.Serialization.Converters;

internal sealed class YamlInt32Converter : YamlConverter<int>
{
    public static YamlInt32Converter Instance { get; } = new();

    public override int Read(YamlReader reader)
    {
        if (reader.TokenType != YamlTokenType.Scalar)
        {
            throw YamlThrowHelper.ThrowExpectedScalar(reader);
        }

        var value = reader.ScalarValue.AsSpan();
        if (!YamlScalar.TryParseInt32(value, out var result))
        {
            throw YamlThrowHelper.ThrowInvalidIntegerScalar(reader);
        }

        reader.Read();
        return result;
    }

    public override void Write(YamlWriter writer, int value)
    {
        writer.WriteScalar(value.ToString(System.Globalization.CultureInfo.InvariantCulture));
    }
}
