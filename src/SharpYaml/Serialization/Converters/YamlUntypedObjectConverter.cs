using System;
using System.Collections.Generic;

namespace SharpYaml.Serialization.Converters;

internal sealed class YamlUntypedObjectConverter : YamlConverter
{
    public static YamlUntypedObjectConverter Instance { get; } = new();

    public override bool CanConvert(Type typeToConvert) => typeToConvert == typeof(object);

    public override object? Read(YamlReader reader, Type typeToConvert)
    {
        if (reader.TryReadAlias(out var rootAliasValue))
        {
            return rootAliasValue;
        }

        var options = reader.Options;
        if (options.UnsafeAllowDeserializeFromTagTypeName && reader.Tag is not null)
        {
            var activated = TryReadUnsafeTaggedValue(reader);
            if (activated is not null)
            {
                return activated;
            }
        }

        switch (reader.TokenType)
        {
            case YamlTokenType.Scalar:
                var text = reader.ScalarValue.AsSpan();
                if (YamlScalar.IsNull(text))
                {
                    reader.Read();
                    return null;
                }

                if (YamlScalar.TryParseBool(text, out var boolean))
                {
                    reader.Read();
                    return boolean;
                }

                if (YamlScalar.TryParseInt64(text, out var integer))
                {
                    reader.Read();
                    return integer;
                }

                if (YamlScalar.TryParseDouble(text, out var floating))
                {
                    reader.Read();
                    return floating;
                }

                var str = reader.ScalarValue ?? string.Empty;
                reader.Read();
                return str;

            case YamlTokenType.StartSequence:
                var sequenceAnchor = reader.Anchor;
                reader.Read();
                var list = new List<object?>();
                if (reader.ReferenceReader is not null && sequenceAnchor is not null)
                {
                    reader.ReferenceReader.Register(sequenceAnchor, list);
                }

                while (reader.TokenType != YamlTokenType.EndSequence)
                {
                    list.Add(Read(reader, typeof(object)));
                }
                reader.Read();
                return list;

            case YamlTokenType.StartMapping:
                var mappingAnchor = reader.Anchor;
                reader.Read();
                var dict = new Dictionary<string, object?>(options.PropertyNameCaseInsensitive ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal);
                if (reader.ReferenceReader is not null && mappingAnchor is not null)
                {
                    reader.ReferenceReader.Register(mappingAnchor, dict);
                }

                while (reader.TokenType != YamlTokenType.EndMapping)
                {
                    if (reader.TokenType != YamlTokenType.Scalar)
                    {
                        throw YamlThrowHelper.ThrowExpectedScalarKey(reader);
                    }

                    var key = reader.ScalarValue ?? string.Empty;
                    reader.Read();
                    var value = Read(reader, typeof(object));

                    if (dict.ContainsKey(key))
                    {
                        switch (options.DuplicateKeyHandling)
                        {
                            case YamlDuplicateKeyHandling.Error:
                                throw YamlThrowHelper.ThrowDuplicateMappingKey(reader, key);
                            case YamlDuplicateKeyHandling.FirstWins:
                                break;
                            case YamlDuplicateKeyHandling.LastWins:
                                dict[key] = value;
                                break;
                        }
                    }
                    else
                    {
                        dict[key] = value;
                    }
                }
                reader.Read();
                return dict;

            case YamlTokenType.Alias:
                throw new YamlException(reader.SourceName, reader.Start, reader.End, "Aliases are not supported when deserializing into object unless ReferenceHandling is Preserve.");

            default:
                throw YamlThrowHelper.ThrowUnexpectedToken(reader);
        }
    }

    public override void Write(YamlWriter writer, object? value)
    {
        if (value is null)
        {
            writer.WriteNullValue();
            return;
        }

        var converter = writer.GetConverter(value.GetType());
        converter.Write(writer, value);
    }

    private object? TryReadUnsafeTaggedValue(YamlReader reader)
    {
        var tag = reader.Tag;
        if (string.IsNullOrWhiteSpace(tag) || tag[0] != '!')
        {
            return null;
        }

        var typeName = tag.Substring(1);
        var type = Type.GetType(typeName, throwOnError: false);
        if (type is null && typeName.Contains(",mscorlib", StringComparison.Ordinal))
        {
            type = Type.GetType(typeName.Replace(",mscorlib", ",System.Private.CoreLib", StringComparison.Ordinal), throwOnError: false);
        }

        if (type is null)
        {
            return null;
        }

        var converter = reader.GetConverter(type);
        return converter.Read(reader, type);
    }
}
