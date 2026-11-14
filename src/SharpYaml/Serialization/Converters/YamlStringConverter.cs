// // Copyright (c) Alexandre Mutel. All rights reserved.
// // Licensed under the MIT license.
// // See LICENSE.txt file in the project root for full license information.

namespace SharpYaml.Serialization.Converters;

internal sealed class YamlStringConverter : YamlConverter<string?>
{
    public static YamlStringConverter Instance { get; } = new();

    public override string? Read(YamlReader reader)
    {
        if (reader.TokenType == YamlTokenType.Scalar)
        {
            var value = reader.ScalarValue ?? string.Empty;
            if (YamlScalar.IsNull(reader))
            {
                reader.Read();
                return null;
            }

            reader.Read();
            return value;
        }

        throw YamlThrowHelper.ThrowExpectedScalar(reader);
    }

    public override void Write(YamlWriter writer, string? value)
    {
        writer.WriteString(value);
    }
}
