using System;

namespace SharpYaml.Serialization.Converters;

internal interface IYamlConverterResolver
{
    YamlConverter GetConverter(Type typeToConvert);
}

