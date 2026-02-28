using System;

namespace SharpYaml.Serialization.Converters;

internal sealed class YamlDoubleConverter : YamlConverter<double>
{
    public static YamlDoubleConverter Instance { get; } = new();

    public override double Read(ref YamlReader reader, YamlSerializerOptions options)
    {
        if (reader.TokenType != YamlTokenType.Scalar)
        {
            throw YamlThrowHelper.ThrowExpectedScalar(ref reader);
        }

        var value = reader.ScalarValue.AsSpan();
        if (!YamlScalar.TryParseDouble(value, out var result))
        {
            throw YamlThrowHelper.ThrowInvalidFloatScalar(ref reader);
        }

        reader.Read();
        return result;
    }

    public override void Write(YamlWriter writer, double value, YamlSerializerOptions options)
    {
        if (double.IsPositiveInfinity(value))
        {
            writer.WriteScalar(".inf");
            return;
        }

        if (double.IsNegativeInfinity(value))
        {
            writer.WriteScalar("-.inf");
            return;
        }

        if (double.IsNaN(value))
        {
            writer.WriteScalar(".nan");
            return;
        }

        writer.WriteScalar(value.ToString("R", System.Globalization.CultureInfo.InvariantCulture));
    }
}
