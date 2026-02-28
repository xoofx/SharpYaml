using System;
using System.Globalization;

namespace SharpYaml.Serialization.Converters;

internal static class YamlScalarParser
{
    public static bool IsNull(ReadOnlySpan<char> value)
    {
        value = Trim(value);
        if (value.Length == 0)
        {
            return true;
        }

        if (value.Length == 1 && value[0] == '~')
        {
            return true;
        }

        return value.Equals("null", StringComparison.OrdinalIgnoreCase);
    }

    public static bool TryParseBool(ReadOnlySpan<char> value, out bool result)
    {
        value = Trim(value);
        if (value.Equals("true", StringComparison.OrdinalIgnoreCase))
        {
            result = true;
            return true;
        }

        if (value.Equals("false", StringComparison.OrdinalIgnoreCase))
        {
            result = false;
            return true;
        }

        result = default;
        return false;
    }

    public static bool TryParseInt32(ReadOnlySpan<char> value, out int result)
    {
        if (TryParseInt64(value, out var longValue) && longValue is >= int.MinValue and <= int.MaxValue)
        {
            result = (int)longValue;
            return true;
        }

        result = default;
        return false;
    }

    public static bool TryParseUInt32(ReadOnlySpan<char> value, out uint result)
    {
        if (TryParseUInt64(value, out var ulongValue) && ulongValue <= uint.MaxValue)
        {
            result = (uint)ulongValue;
            return true;
        }

        result = default;
        return false;
    }

    public static bool TryParseUInt64(ReadOnlySpan<char> value, out ulong result)
    {
        value = Trim(value);
        if (value.Length == 0)
        {
            result = default;
            return false;
        }

        if (value[0] == '-')
        {
            result = default;
            return false;
        }

        // Remove underscores (allocate to avoid stack-spans escaping analysis).
        ReadOnlySpan<char> cleaned = value;
        if (value.Contains('_'))
        {
            var buffer = new char[value.Length];
            var written = 0;
            for (var i = 0; i < value.Length; i++)
            {
                var c = value[i];
                if (c != '_')
                {
                    buffer[written++] = c;
                }
            }

            var underscoreRemoved = new string(buffer, 0, written);
            cleaned = underscoreRemoved.AsSpan();
        }

        if (cleaned.Length > 0 && cleaned[0] == '+')
        {
            cleaned = cleaned.Slice(1);
        }

        if (cleaned.Length >= 2 && cleaned[0] == '0')
        {
            var prefix = cleaned[1];
            if (prefix is 'x' or 'X')
            {
                return TryParseUInt64Base(cleaned.Slice(2), 16, out result);
            }

            if (prefix is 'o' or 'O')
            {
                return TryParseUInt64Base(cleaned.Slice(2), 8, out result);
            }

            if (prefix is 'b' or 'B')
            {
                return TryParseUInt64Base(cleaned.Slice(2), 2, out result);
            }
        }

        return ulong.TryParse(cleaned, NumberStyles.None, CultureInfo.InvariantCulture, out result);
    }

    public static bool TryParseDecimal(ReadOnlySpan<char> value, out decimal result)
    {
        value = Trim(value);
        if (value.Length == 0)
        {
            result = default;
            return false;
        }

        if (value.Equals(".inf", StringComparison.OrdinalIgnoreCase) ||
            value.Equals("+.inf", StringComparison.OrdinalIgnoreCase) ||
            value.Equals("-.inf", StringComparison.OrdinalIgnoreCase) ||
            value.Equals(".nan", StringComparison.OrdinalIgnoreCase))
        {
            result = default;
            return false;
        }

        if (value.Contains('_'))
        {
            var buffer = new char[value.Length];
            var written = 0;
            for (var i = 0; i < value.Length; i++)
            {
                var c = value[i];
                if (c != '_')
                {
                    buffer[written++] = c;
                }
            }

            var underscoreRemoved = new string(buffer, 0, written);
            return decimal.TryParse(underscoreRemoved.AsSpan(), NumberStyles.Float, CultureInfo.InvariantCulture, out result);
        }

        return decimal.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out result);
    }

    public static bool TryParseInt64(ReadOnlySpan<char> value, out long result)
    {
        value = Trim(value);
        if (value.Length == 0)
        {
            result = default;
            return false;
        }

        // Remove underscores (allocate to avoid stack-spans escaping analysis).
        ReadOnlySpan<char> cleaned = value;
        string? underscoreRemoved = null;
        if (value.Contains('_'))
        {
            var buffer = new char[value.Length];
            var written = 0;
            for (var i = 0; i < value.Length; i++)
            {
                var c = value[i];
                if (c != '_')
                {
                    buffer[written++] = c;
                }
            }

            underscoreRemoved = new string(buffer, 0, written);
            cleaned = underscoreRemoved.AsSpan();
        }

        var sign = 1;
        if (cleaned.Length > 0 && (cleaned[0] == '+' || cleaned[0] == '-'))
        {
            if (cleaned[0] == '-')
            {
                sign = -1;
            }
            cleaned = cleaned.Slice(1);
        }

        if (cleaned.Length >= 2 && cleaned[0] == '0')
        {
            var prefix = cleaned[1];
            if (prefix is 'x' or 'X')
            {
                return TryParseInt64Base(cleaned.Slice(2), 16, sign, out result);
            }

            if (prefix is 'o' or 'O')
            {
                return TryParseInt64Base(cleaned.Slice(2), 8, sign, out result);
            }

            if (prefix is 'b' or 'B')
            {
                return TryParseInt64Base(cleaned.Slice(2), 2, sign, out result);
            }
        }

        if (!long.TryParse(cleaned, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out result))
        {
            result = default;
            return false;
        }

        result *= sign;
        return true;
    }

    public static bool TryParseDouble(ReadOnlySpan<char> value, out double result)
    {
        value = Trim(value);

        if (value.Equals(".inf", StringComparison.OrdinalIgnoreCase) || value.Equals("+.inf", StringComparison.OrdinalIgnoreCase))
        {
            result = double.PositiveInfinity;
            return true;
        }

        if (value.Equals("-.inf", StringComparison.OrdinalIgnoreCase))
        {
            result = double.NegativeInfinity;
            return true;
        }

        if (value.Equals(".nan", StringComparison.OrdinalIgnoreCase))
        {
            result = double.NaN;
            return true;
        }

        if (value.Contains('_'))
        {
            var buffer = new char[value.Length];
            var written = 0;
            for (var i = 0; i < value.Length; i++)
            {
                var c = value[i];
                if (c != '_')
                {
                    buffer[written++] = c;
                }
            }

            var underscoreRemovedDouble = new string(buffer, 0, written);
            return double.TryParse(underscoreRemovedDouble.AsSpan(), NumberStyles.Float, CultureInfo.InvariantCulture, out result);
        }

        return double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out result);
    }

    private static ReadOnlySpan<char> Trim(ReadOnlySpan<char> value)
    {
        while (value.Length > 0 && char.IsWhiteSpace(value[0]))
        {
            value = value.Slice(1);
        }

        while (value.Length > 0 && char.IsWhiteSpace(value[^1]))
        {
            value = value.Slice(0, value.Length - 1);
        }

        return value;
    }

    private static bool TryParseInt64Base(ReadOnlySpan<char> value, int numberBase, int sign, out long result)
    {
        if (value.Length == 0)
        {
            result = default;
            return false;
        }

        long accumulator = 0;
        for (var i = 0; i < value.Length; i++)
        {
            var c = value[i];
            int digit;
            if (c is >= '0' and <= '9')
            {
                digit = c - '0';
            }
            else if (c is >= 'a' and <= 'f')
            {
                digit = 10 + (c - 'a');
            }
            else if (c is >= 'A' and <= 'F')
            {
                digit = 10 + (c - 'A');
            }
            else
            {
                result = default;
                return false;
            }

            if (digit >= numberBase)
            {
                result = default;
                return false;
            }

            checked
            {
                accumulator = (accumulator * numberBase) + digit;
            }
        }

        result = accumulator * sign;
        return true;
    }

    private static bool TryParseUInt64Base(ReadOnlySpan<char> value, int numberBase, out ulong result)
    {
        if (value.Length == 0)
        {
            result = default;
            return false;
        }

        ulong accumulator = 0;
        for (var i = 0; i < value.Length; i++)
        {
            var c = value[i];
            int digit;
            if (c is >= '0' and <= '9')
            {
                digit = c - '0';
            }
            else if (c is >= 'a' and <= 'f')
            {
                digit = 10 + (c - 'a');
            }
            else if (c is >= 'A' and <= 'F')
            {
                digit = 10 + (c - 'A');
            }
            else
            {
                result = default;
                return false;
            }

            if (digit >= numberBase)
            {
                result = default;
                return false;
            }

            checked
            {
                accumulator = (accumulator * (ulong)numberBase) + (ulong)digit;
            }
        }

        result = accumulator;
        return true;
    }
}
