using System;
using SharpYaml.Serialization;

namespace SharpYaml;

internal sealed class CaseInsensitiveDefaultNamingConvention : IMemberNamingConvention
{
    public StringComparer Comparer => StringComparer.OrdinalIgnoreCase;

    public string Convert(string name)
    {
        return name;
    }
}

