// // Copyright (c) Alexandre Mutel. All rights reserved.
// // Licensed under the MIT license.
// // See LICENSE.txt file in the project root for full license information.

#if NET7_0_OR_GREATER
using System;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;

namespace SharpYaml.Serialization.Converters;

internal sealed class YamlParsableConverterFactory : YamlConverterFactory
{
    public static YamlParsableConverterFactory Instance { get; } = new();

    [UnconditionalSuppressMessage("Trimming", "IL2067", Justification = "This code path is only used by reflection-based serialization.")]
    public override bool CanConvert(Type typeToConvert)
    {
        return HasIParsable(typeToConvert);
    }

    [UnconditionalSuppressMessage("AOT", "IL3050", Justification = "This code path is only used by reflection-based serialization.")]
    [UnconditionalSuppressMessage("Trimming", "IL2067", Justification = "This code path is only used by reflection-based serialization.")]
    public override YamlConverter? CreateConverter(Type typeToConvert, YamlSerializerOptions options)
    {
        if (!HasIParsable(typeToConvert))
        {
            return null;
        }

        var converterType = typeof(YamlParsableConverter<>).MakeGenericType(typeToConvert);
        return (YamlConverter)Activator.CreateInstance(converterType)!;
    }

    private static bool HasIParsable(
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.Interfaces)] Type type)
    {
        foreach (var iface in type.GetInterfaces())
        {
            if (iface.IsGenericType && iface.GetGenericTypeDefinition() == typeof(IParsable<>)
                && iface.GetGenericArguments()[0] == type)
            {
                return true;
            }
        }

        return false;
    }
}

internal sealed class YamlParsableConverter<T> : YamlConverter<T> where T : IParsable<T>
{
    public override T Read(YamlReader reader)
    {
        if (reader.TokenType != YamlTokenType.Scalar)
        {
            throw YamlThrowHelper.ThrowExpectedScalar(reader);
        }

        var text = reader.ScalarValue!;
        reader.Read();

        if (!T.TryParse(text, CultureInfo.InvariantCulture, out var result))
        {
            throw new YamlException(reader.SourceName, reader.Start, reader.End, $"Cannot parse '{text}' as {typeof(T).Name}.");
        }

        return result;
    }

    public override void Write(YamlWriter writer, T value)
    {
        string text;
        if (value is IFormattable formattable)
        {
            text = formattable.ToString(null, CultureInfo.InvariantCulture);
        }
        else
        {
            text = value?.ToString() ?? string.Empty;
        }

        writer.WriteScalar(text);
    }
}
#endif
