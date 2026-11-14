// Copyright (c) Alexandre Mutel. All rights reserved.
// Licensed under the MIT license.

using System.Globalization;
using System.Numerics;

namespace SharpYaml.Serialization.Converters;

internal sealed class YamlBigIntegerConverter : YamlConverter<BigInteger>
{
    public static YamlBigIntegerConverter Instance { get; } = new();

    public override BigInteger Read(YamlReader reader)
    {
        if (reader.TokenType != YamlTokenType.Scalar) throw YamlThrowHelper.ThrowExpectedScalar(reader);
        if (!YamlScalar.TryParseBigInteger(reader, out var value)) throw YamlThrowHelper.ThrowInvalidIntegerScalar(reader);
        reader.Read();
        return value;
    }

    public override void Write(YamlWriter writer, BigInteger value) => writer.WriteScalar(value.ToString(CultureInfo.InvariantCulture));
}
