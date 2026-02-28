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
    public static YamlException ThrowExpectedToken(ref YamlReader reader, YamlTokenType expectedToken)
        => new(reader.SourceName, reader.Start, reader.End, $"Expected a {expectedToken} token but found '{reader.TokenType}'.");

    public static YamlException ThrowExpectedScalar(ref YamlReader reader)
        => ThrowExpectedToken(ref reader, YamlTokenType.Scalar);

    public static YamlException ThrowExpectedScalarKey(ref YamlReader reader)
        => new(reader.SourceName, reader.Start, reader.End, $"Expected a scalar key token but found '{reader.TokenType}'.");

    public static YamlException ThrowExpectedMapping(ref YamlReader reader)
        => ThrowExpectedToken(ref reader, YamlTokenType.StartMapping);

    public static YamlException ThrowExpectedSequence(ref YamlReader reader)
        => ThrowExpectedToken(ref reader, YamlTokenType.StartSequence);

    public static YamlException ThrowUnexpectedToken(ref YamlReader reader)
        => new(reader.SourceName, reader.Start, reader.End, $"Unexpected token '{reader.TokenType}'.");

    public static YamlException ThrowNotSupported(ref YamlReader reader, string message)
        => new(reader.SourceName, reader.Start, reader.End, message);

    public static YamlException ThrowDuplicateMappingKey(ref YamlReader reader, string key)
        => new(reader.SourceName, reader.Start, reader.End, $"Duplicate mapping key '{key}'.");

    public static YamlException ThrowUnknownTypeDiscriminator(ref YamlReader reader, string? discriminatorValue, Type baseType)
        => new(reader.SourceName, reader.Start, reader.End, $"Unknown type discriminator '{discriminatorValue}' for '{baseType}'.");

    public static YamlException ThrowAbstractTypeWithoutDiscriminator(ref YamlReader reader, Type type)
        => new(reader.SourceName, reader.Start, reader.End, $"Cannot deserialize abstract type '{type}' without a known derived type discriminator.");

    public static YamlException ThrowExpectedDiscriminatorScalar(ref YamlReader reader, string discriminatorPropertyName)
        => new(reader.SourceName, reader.Start, reader.End, $"Expected '{discriminatorPropertyName}' to be a scalar discriminator.");

    public static YamlException ThrowInvalidScalar(ref YamlReader reader, string message)
        => new(reader.SourceName, reader.Start, reader.End, message);

    public static YamlException ThrowInvalidBooleanScalar(ref YamlReader reader)
        => ThrowInvalidScalar(ref reader, $"Invalid boolean scalar '{reader.ScalarValue}'.");

    public static YamlException ThrowInvalidIntegerScalar(ref YamlReader reader)
        => ThrowInvalidScalar(ref reader, $"Invalid integer scalar '{reader.ScalarValue}'.");

    public static YamlException ThrowInvalidUInt32Scalar(ref YamlReader reader)
        => ThrowInvalidScalar(ref reader, $"Invalid uint32 scalar '{reader.ScalarValue}'.");

    public static YamlException ThrowInvalidUInt64Scalar(ref YamlReader reader)
        => ThrowInvalidScalar(ref reader, $"Invalid uint64 scalar '{reader.ScalarValue}'.");

    public static YamlException ThrowInvalidByteScalar(ref YamlReader reader)
        => ThrowInvalidScalar(ref reader, $"Invalid byte scalar '{reader.ScalarValue}'.");

    public static YamlException ThrowInvalidSByteScalar(ref YamlReader reader)
        => ThrowInvalidScalar(ref reader, $"Invalid sbyte scalar '{reader.ScalarValue}'.");

    public static YamlException ThrowInvalidInt16Scalar(ref YamlReader reader)
        => ThrowInvalidScalar(ref reader, $"Invalid int16 scalar '{reader.ScalarValue}'.");

    public static YamlException ThrowInvalidUInt16Scalar(ref YamlReader reader)
        => ThrowInvalidScalar(ref reader, $"Invalid uint16 scalar '{reader.ScalarValue}'.");

    public static YamlException ThrowInvalidNIntScalar(ref YamlReader reader)
        => ThrowInvalidScalar(ref reader, $"Invalid nint scalar '{reader.ScalarValue}'.");

    public static YamlException ThrowInvalidNUIntScalar(ref YamlReader reader)
        => ThrowInvalidScalar(ref reader, $"Invalid nuint scalar '{reader.ScalarValue}'.");

    public static YamlException ThrowInvalidFloatScalar(ref YamlReader reader)
        => ThrowInvalidScalar(ref reader, $"Invalid float scalar '{reader.ScalarValue}'.");

    public static YamlException ThrowInvalidDecimalScalar(ref YamlReader reader)
        => ThrowInvalidScalar(ref reader, $"Invalid decimal scalar '{reader.ScalarValue}'.");

    public static YamlException ThrowInvalidCharScalar(ref YamlReader reader, string text)
        => ThrowInvalidScalar(ref reader, $"Invalid char scalar '{text}'.");

    public static YamlException ThrowInvalidEnumScalar(ref YamlReader reader, string text)
        => ThrowInvalidScalar(ref reader, $"Invalid enum scalar '{text}'.");

    public static YamlException ThrowAliasMissingValue(ref YamlReader reader)
        => new(reader.SourceName, reader.Start, reader.End, "Alias token did not provide an alias value.");
}
