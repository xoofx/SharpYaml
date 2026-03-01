// // Copyright (c) Alexandre Mutel. All rights reserved.
// // Licensed under the MIT license.
// // See LICENSE.txt file in the project root for full license information.

using System;
using System.Diagnostics.CodeAnalysis;

namespace SharpYaml.Serialization;

internal static class YamlSerializerFeatureSwitches
{
    internal const string ReflectionSwitchName = "SharpYaml.YamlSerializer.IsReflectionEnabledByDefault";

    // This property is stubbed by ILLink.Substitutions.xml when the feature switch is disabled.
#if NET10_0_OR_GREATER
    [FeatureSwitchDefinition(ReflectionSwitchName)]
#endif
    public static bool IsReflectionEnabledByDefault
        => AppContext.TryGetSwitch(ReflectionSwitchName, out var enabled) ? enabled : true;

    public static readonly bool IsReflectionEnabledByDefaultCalculated = IsReflectionEnabledByDefault;
}
