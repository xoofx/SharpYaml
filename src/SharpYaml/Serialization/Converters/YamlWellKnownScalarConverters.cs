// // Copyright (c) Alexandre Mutel. All rights reserved.
// // Licensed under the MIT license.
// // See LICENSE.txt file in the project root for full license information.

using System;
using System.Globalization;

namespace SharpYaml.Serialization.Converters;

internal sealed class YamlDateTimeConverter : YamlConverter<DateTime>
{
    public static YamlDateTimeConverter Instance { get; } = new();

    public override DateTime Read(YamlReader reader)
    {
        if (reader.TokenType != YamlTokenType.Scalar)
        {
            throw YamlThrowHelper.ThrowExpectedScalar(reader);
        }

        var text = reader.ScalarValue;
        if (!DateTime.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var result))
        {
            throw YamlThrowHelper.ThrowInvalidDateTimeScalar(reader);
        }

        reader.Read();
        return result;
    }

    public override void Write(YamlWriter writer, DateTime value)
    {
        writer.WriteScalar(value.ToString("O", CultureInfo.InvariantCulture));
    }
}

internal sealed class YamlDateTimeOffsetConverter : YamlConverter<DateTimeOffset>
{
    public static YamlDateTimeOffsetConverter Instance { get; } = new();

    public override DateTimeOffset Read(YamlReader reader)
    {
        if (reader.TokenType != YamlTokenType.Scalar)
        {
            throw YamlThrowHelper.ThrowExpectedScalar(reader);
        }

        var text = reader.ScalarValue;
        if (!DateTimeOffset.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var result))
        {
            throw YamlThrowHelper.ThrowInvalidDateTimeOffsetScalar(reader);
        }

        reader.Read();
        return result;
    }

    public override void Write(YamlWriter writer, DateTimeOffset value)
    {
        writer.WriteScalar(value.ToString("O", CultureInfo.InvariantCulture));
    }
}

internal sealed class YamlGuidConverter : YamlConverter<Guid>
{
    public static YamlGuidConverter Instance { get; } = new();

    public override Guid Read(YamlReader reader)
    {
        if (reader.TokenType != YamlTokenType.Scalar)
        {
            throw YamlThrowHelper.ThrowExpectedScalar(reader);
        }

        var text = reader.ScalarValue;
        if (!Guid.TryParse(text, out var result))
        {
            throw YamlThrowHelper.ThrowInvalidGuidScalar(reader);
        }

        reader.Read();
        return result;
    }

    public override void Write(YamlWriter writer, Guid value)
    {
        writer.WriteScalar(value.ToString("D"));
    }
}

internal sealed class YamlTimeSpanConverter : YamlConverter<TimeSpan>
{
    public static YamlTimeSpanConverter Instance { get; } = new();

    public override TimeSpan Read(YamlReader reader)
    {
        if (reader.TokenType != YamlTokenType.Scalar)
        {
            throw YamlThrowHelper.ThrowExpectedScalar(reader);
        }

        var text = reader.ScalarValue;
        if (!TimeSpan.TryParse(text, CultureInfo.InvariantCulture, out var result))
        {
            throw YamlThrowHelper.ThrowInvalidTimeSpanScalar(reader);
        }

        reader.Read();
        return result;
    }

    public override void Write(YamlWriter writer, TimeSpan value)
    {
        writer.WriteScalar(value.ToString("c", CultureInfo.InvariantCulture));
    }
}

#if NET6_0_OR_GREATER
internal sealed class YamlDateOnlyConverter : YamlConverter<DateOnly>
{
    public static YamlDateOnlyConverter Instance { get; } = new();

    public override DateOnly Read(YamlReader reader)
    {
        if (reader.TokenType != YamlTokenType.Scalar)
        {
            throw YamlThrowHelper.ThrowExpectedScalar(reader);
        }

        var text = reader.ScalarValue;
        if (!DateOnly.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.None, out var result))
        {
            throw YamlThrowHelper.ThrowInvalidDateOnlyScalar(reader);
        }

        reader.Read();
        return result;
    }

    public override void Write(YamlWriter writer, DateOnly value)
    {
        writer.WriteScalar(value.ToString("O", CultureInfo.InvariantCulture));
    }
}

internal sealed class YamlTimeOnlyConverter : YamlConverter<TimeOnly>
{
    public static YamlTimeOnlyConverter Instance { get; } = new();

    public override TimeOnly Read(YamlReader reader)
    {
        if (reader.TokenType != YamlTokenType.Scalar)
        {
            throw YamlThrowHelper.ThrowExpectedScalar(reader);
        }

        var text = reader.ScalarValue;
        if (!TimeOnly.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.None, out var result))
        {
            throw YamlThrowHelper.ThrowInvalidTimeOnlyScalar(reader);
        }

        reader.Read();
        return result;
    }

    public override void Write(YamlWriter writer, TimeOnly value)
    {
        writer.WriteScalar(value.ToString("O", CultureInfo.InvariantCulture));
    }
}
#endif

#if NET5_0_OR_GREATER
internal sealed class YamlHalfConverter : YamlConverter<Half>
{
    public static YamlHalfConverter Instance { get; } = new();

    public override Half Read(YamlReader reader)
    {
        if (reader.TokenType != YamlTokenType.Scalar)
        {
            throw YamlThrowHelper.ThrowExpectedScalar(reader);
        }

        var text = reader.ScalarValue;
        if (!Half.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var result))
        {
            throw YamlThrowHelper.ThrowInvalidHalfScalar(reader);
        }

        reader.Read();
        return result;
    }

    public override void Write(YamlWriter writer, Half value)
    {
        writer.WriteScalar(value.ToString(CultureInfo.InvariantCulture));
    }
}
#endif

#if NET7_0_OR_GREATER
internal sealed class YamlInt128Converter : YamlConverter<Int128>
{
    public static YamlInt128Converter Instance { get; } = new();

    public override Int128 Read(YamlReader reader)
    {
        if (reader.TokenType != YamlTokenType.Scalar)
        {
            throw YamlThrowHelper.ThrowExpectedScalar(reader);
        }

        var text = reader.ScalarValue;
        if (!Int128.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var result))
        {
            throw YamlThrowHelper.ThrowInvalidInt128Scalar(reader);
        }

        reader.Read();
        return result;
    }

    public override void Write(YamlWriter writer, Int128 value)
    {
        writer.WriteScalar(value.ToString(CultureInfo.InvariantCulture));
    }
}

internal sealed class YamlUInt128Converter : YamlConverter<UInt128>
{
    public static YamlUInt128Converter Instance { get; } = new();

    public override UInt128 Read(YamlReader reader)
    {
        if (reader.TokenType != YamlTokenType.Scalar)
        {
            throw YamlThrowHelper.ThrowExpectedScalar(reader);
        }

        var text = reader.ScalarValue;
        if (!UInt128.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var result))
        {
            throw YamlThrowHelper.ThrowInvalidUInt128Scalar(reader);
        }

        reader.Read();
        return result;
    }

    public override void Write(YamlWriter writer, UInt128 value)
    {
        writer.WriteScalar(value.ToString(CultureInfo.InvariantCulture));
    }
}
#endif
