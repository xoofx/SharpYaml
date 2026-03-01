// // Copyright (c) Alexandre Mutel. All rights reserved.
// // Licensed under the MIT license.
// // See LICENSE.txt file in the project root for full license information.

using System;
using SharpYaml.Serialization;

namespace SharpYaml;

/// <summary>
/// Provides reflection-based type metadata for <see cref="YamlSerializer"/>.
/// </summary>
public sealed class ReflectionYamlTypeInfoResolver : IYamlTypeInfoResolver
{
    private sealed class ReflectionYamlTypeInfo : YamlTypeInfo
    {
        public ReflectionYamlTypeInfo(Type type, YamlSerializerOptions options) : base(type, options)
        {
        }

        public override void Write(YamlWriter writer, object? value)
        {
            ArgumentGuard.ThrowIfNull(writer);
            writer.GetConverter(Type).Write(writer, value);
        }

        public override object? ReadAsObject(YamlReader reader)
        {
            ArgumentGuard.ThrowIfNull(reader);
            return reader.GetConverter(Type).Read(reader, Type);
        }
    }

    /// <summary>
    /// Gets a shared default reflection resolver instance.
    /// </summary>
    public static ReflectionYamlTypeInfoResolver Default { get; } = new();

    /// <inheritdoc />
    public YamlTypeInfo? GetTypeInfo(Type type, YamlSerializerOptions options)
    {
        ArgumentGuard.ThrowIfNull(type);
        ArgumentGuard.ThrowIfNull(options);
        return new ReflectionYamlTypeInfo(type, options);
    }
}
