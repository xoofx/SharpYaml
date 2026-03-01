using System;

namespace SharpYaml.Serialization;

/// <summary>
/// Helper methods for throwing <see cref="YamlException"/> with location context.
/// </summary>
/// <remarks>
/// This type is public so that source-generated serializers can reuse centralized exception logic
/// without duplicating message formatting.
/// </remarks>
public static class YamlThrowHelper
{
    /// <summary>Throws an exception for expected Token.</summary>
    public static YamlException ThrowExpectedToken(YamlReader reader, YamlTokenType expectedToken)
        => new(reader.SourceName, reader.Start, reader.End, $"Expected a {expectedToken} token but found '{reader.TokenType}'.");

    /// <summary>Throws an exception for expected Scalar.</summary>
    public static YamlException ThrowExpectedScalar(YamlReader reader)
        => ThrowExpectedToken(reader, YamlTokenType.Scalar);

    /// <summary>Throws an exception for expected Scalar Key.</summary>
    public static YamlException ThrowExpectedScalarKey(YamlReader reader)
        => new(reader.SourceName, reader.Start, reader.End, $"Expected a scalar key token but found '{reader.TokenType}'.");

    /// <summary>Throws an exception for expected Mapping.</summary>
    public static YamlException ThrowExpectedMapping(YamlReader reader)
        => ThrowExpectedToken(reader, YamlTokenType.StartMapping);

    /// <summary>Throws an exception for expected Sequence.</summary>
    public static YamlException ThrowExpectedSequence(YamlReader reader)
        => ThrowExpectedToken(reader, YamlTokenType.StartSequence);

    /// <summary>Throws an exception for unexpected Token.</summary>
    public static YamlException ThrowUnexpectedToken(YamlReader reader)
        => new(reader.SourceName, reader.Start, reader.End, $"Unexpected token '{reader.TokenType}'.");

    /// <summary>Throws an exception for not Supported.</summary>
    public static YamlException ThrowNotSupported(YamlReader reader, string message)
        => new(reader.SourceName, reader.Start, reader.End, message);

    /// <summary>Throws an exception for duplicate Mapping Key.</summary>
    public static YamlException ThrowDuplicateMappingKey(YamlReader reader, string key)
        => new(reader.SourceName, reader.Start, reader.End, $"Duplicate mapping key '{key}'.");

    /// <summary>Throws an exception for unknown Type Discriminator.</summary>
    public static YamlException ThrowUnknownTypeDiscriminator(YamlReader reader, string? discriminatorValue, Type baseType)
        => new(reader.SourceName, reader.Start, reader.End, $"Unknown type discriminator '{discriminatorValue}' for '{baseType}'.");

    /// <summary>Throws an exception for abstract Type Without Discriminator.</summary>
    public static YamlException ThrowAbstractTypeWithoutDiscriminator(YamlReader reader, Type type)
        => new(reader.SourceName, reader.Start, reader.End, $"Cannot deserialize abstract type '{type}' without a known derived type discriminator.");

    /// <summary>Throws an exception for expected Discriminator Scalar.</summary>
    public static YamlException ThrowExpectedDiscriminatorScalar(YamlReader reader, string discriminatorPropertyName)
        => new(reader.SourceName, reader.Start, reader.End, $"Expected '{discriminatorPropertyName}' to be a scalar discriminator.");

    /// <summary>Throws an exception for invalid Scalar.</summary>
    public static YamlException ThrowInvalidScalar(YamlReader reader, string message)
        => new(reader.SourceName, reader.Start, reader.End, message);

    /// <summary>Throws an exception for invalid Boolean Scalar.</summary>
    public static YamlException ThrowInvalidBooleanScalar(YamlReader reader)
        => ThrowInvalidScalar(reader, $"Invalid boolean scalar '{reader.ScalarValue}'.");

    /// <summary>Throws an exception for invalid Integer Scalar.</summary>
    public static YamlException ThrowInvalidIntegerScalar(YamlReader reader)
        => ThrowInvalidScalar(reader, $"Invalid integer scalar '{reader.ScalarValue}'.");

    /// <summary>Throws an exception for invalid U Int32 Scalar.</summary>
    public static YamlException ThrowInvalidUInt32Scalar(YamlReader reader)
        => ThrowInvalidScalar(reader, $"Invalid uint32 scalar '{reader.ScalarValue}'.");

    /// <summary>Throws an exception for invalid U Int64 Scalar.</summary>
    public static YamlException ThrowInvalidUInt64Scalar(YamlReader reader)
        => ThrowInvalidScalar(reader, $"Invalid uint64 scalar '{reader.ScalarValue}'.");

    /// <summary>Throws an exception for invalid Byte Scalar.</summary>
    public static YamlException ThrowInvalidByteScalar(YamlReader reader)
        => ThrowInvalidScalar(reader, $"Invalid byte scalar '{reader.ScalarValue}'.");

    /// <summary>Throws an exception for invalid S Byte Scalar.</summary>
    public static YamlException ThrowInvalidSByteScalar(YamlReader reader)
        => ThrowInvalidScalar(reader, $"Invalid sbyte scalar '{reader.ScalarValue}'.");

    /// <summary>Throws an exception for invalid Int16 Scalar.</summary>
    public static YamlException ThrowInvalidInt16Scalar(YamlReader reader)
        => ThrowInvalidScalar(reader, $"Invalid int16 scalar '{reader.ScalarValue}'.");

    /// <summary>Throws an exception for invalid U Int16 Scalar.</summary>
    public static YamlException ThrowInvalidUInt16Scalar(YamlReader reader)
        => ThrowInvalidScalar(reader, $"Invalid uint16 scalar '{reader.ScalarValue}'.");

    /// <summary>Throws an exception for invalid N Int Scalar.</summary>
    public static YamlException ThrowInvalidNIntScalar(YamlReader reader)
        => ThrowInvalidScalar(reader, $"Invalid nint scalar '{reader.ScalarValue}'.");

    /// <summary>Throws an exception for invalid NU Int Scalar.</summary>
    public static YamlException ThrowInvalidNUIntScalar(YamlReader reader)
        => ThrowInvalidScalar(reader, $"Invalid nuint scalar '{reader.ScalarValue}'.");

    /// <summary>Throws an exception for invalid Float Scalar.</summary>
    public static YamlException ThrowInvalidFloatScalar(YamlReader reader)
        => ThrowInvalidScalar(reader, $"Invalid float scalar '{reader.ScalarValue}'.");

    /// <summary>Throws an exception for invalid Decimal Scalar.</summary>
    public static YamlException ThrowInvalidDecimalScalar(YamlReader reader)
        => ThrowInvalidScalar(reader, $"Invalid decimal scalar '{reader.ScalarValue}'.");

    /// <summary>Throws an exception for invalid Char Scalar.</summary>
    public static YamlException ThrowInvalidCharScalar(YamlReader reader, string text)
        => ThrowInvalidScalar(reader, $"Invalid char scalar '{text}'.");

    /// <summary>Throws an exception for invalid Enum Scalar.</summary>
    public static YamlException ThrowInvalidEnumScalar(YamlReader reader, string text)
        => ThrowInvalidScalar(reader, $"Invalid enum scalar '{text}'.");

    /// <summary>Throws an exception for alias Missing Value.</summary>
    public static YamlException ThrowAliasMissingValue(YamlReader reader)
        => new(reader.SourceName, reader.Start, reader.End, "Alias token did not provide an alias value.");
}
