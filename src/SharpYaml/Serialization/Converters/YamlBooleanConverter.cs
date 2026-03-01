// // Copyright (c) Alexandre Mutel. All rights reserved.
// // Licensed under the MIT license.
// // See LICENSE.txt file in the project root for full license information.

using System;

namespace SharpYaml.Serialization.Converters;

internal sealed class YamlBooleanConverter : YamlConverter<bool>
{
    public static YamlBooleanConverter Instance { get; } = new();

    public override bool Read(YamlReader reader)
    {
        if (reader.TokenType != YamlTokenType.Scalar)
        {
            throw YamlThrowHelper.ThrowExpectedScalar(reader);
        }

        var value = reader.ScalarValue.AsSpan();
        if (!YamlScalar.TryParseBool(value, out var result))
        {
            throw YamlThrowHelper.ThrowInvalidBooleanScalar(reader);
        }

        reader.Read();
        return result;
    }

    public override void Write(YamlWriter writer, bool value)
    {
        writer.WriteScalar(value ? "true" : "false");
    }
}
