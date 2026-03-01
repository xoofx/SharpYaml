using System;
using System.Diagnostics.CodeAnalysis;

namespace SharpYaml.Serialization;

internal static class YamlSerializerFeatureSwitches
{
    internal const string ReflectionSwitchName = "SharpYaml.YamlSerializer.IsReflectionEnabledByDefault";

    // This property is stubbed by ILLink.Substitutions.xml when the feature switch is disabled.
#if !NETSTANDARD2_0
    [FeatureSwitchDefinition(ReflectionSwitchName)]
#endif
    public static bool IsReflectionEnabledByDefault
        => AppContext.TryGetSwitch(ReflectionSwitchName, out var enabled) ? enabled : true;

    public static readonly bool IsReflectionEnabledByDefaultCalculated = IsReflectionEnabledByDefault;
}
