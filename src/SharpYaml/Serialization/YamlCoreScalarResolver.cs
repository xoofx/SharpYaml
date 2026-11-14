// Copyright (c) Alexandre Mutel. All rights reserved.
// Licensed under the MIT license.

using System;
using System.Globalization;
using System.Numerics;

namespace SharpYaml.Serialization;

// Recognize §10.3.2 without regex matches or temporary strings on the default path.
internal static class YamlCoreScalarResolver
{
    internal enum Kind { String, Null, Boolean, Integer, Float }

    // Parse an integer already validated against the Core grammar, without a native width limit.
    internal static BigInteger ParseInteger(string text)
    {
        if (text.StartsWith("0x", StringComparison.Ordinal))
        {
            var value = BigInteger.Parse(text.Substring(2), NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture);
            // Hex parsing uses two's complement, whereas a Core hexadecimal integer is unsigned.
            return value.Sign < 0 ? value + (BigInteger.One << checked((text.Length - 2) * 4)) : value;
        }
        if (text.StartsWith("0o", StringComparison.Ordinal))
        {
            var value = BigInteger.Zero;
            uint group = 0;
            var digits = 0;
            for (var i = 2; i < text.Length; i++)
            {
                group = (group << 3) | (uint)(text[i] - '0');
                if (++digits == 9)
                {
                    value = (value << 27) | group;
                    digits = 0;
                    group = 0;
                }
            }
            return (value << (digits * 3)) | group;
        }
        return BigInteger.Parse(text, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture);
    }

    public static Kind Classify(ReadOnlySpan<char> text)
    {
        if (text is "" or "~" or "null" or "Null" or "NULL") return Kind.Null;
        if (text is "true" or "True" or "TRUE" or "false" or "False" or "FALSE") return Kind.Boolean;
        if (text is ".nan" or ".NaN" or ".NAN") return Kind.Float;

        var value = text;
        var offset = value[0] is '+' or '-' ? 1 : 0;
        if (offset == value.Length) return Kind.String;
        var unsigned = value.Slice(offset);
        if (unsigned.SequenceEqual(".inf".AsSpan()) || unsigned.SequenceEqual(".Inf".AsSpan()) || unsigned.SequenceEqual(".INF".AsSpan())) return Kind.Float;

        if (offset == 0 && value.Length > 2 && value[0] == '0' && value[1] is 'o' or 'x')
        {
            var radix = value[1] == 'o' ? 8 : 16;
            for (var i = 2; i < value.Length; i++)
            {
                var c = value[i];
                var digit = c is >= '0' and <= '9' ? c - '0' : c is >= 'a' and <= 'f' ? c - 'a' + 10 : c is >= 'A' and <= 'F' ? c - 'A' + 10 : -1;
                if (digit < 0 || digit >= radix) return Kind.String;
            }
            return Kind.Integer;
        }

        var position = offset;
        while (position < value.Length && value[position] is >= '0' and <= '9') position++;
        var digits = position - offset;
        var floating = false;
        if (position < value.Length && value[position] == '.')
        {
            floating = true;
            position++;
            var fraction = position;
            while (position < value.Length && value[position] is >= '0' and <= '9') position++;
            digits += position - fraction;
        }
        if (digits == 0) return Kind.String;
        if (position < value.Length && value[position] is 'e' or 'E')
        {
            floating = true;
            position++;
            if (position < value.Length && value[position] is '+' or '-') position++;
            var exponent = position;
            while (position < value.Length && value[position] is >= '0' and <= '9') position++;
            if (position == exponent) return Kind.String;
        }
        return position == value.Length ? (floating ? Kind.Float : Kind.Integer) : Kind.String;
    }
}
