// // Copyright (c) Alexandre Mutel. All rights reserved.
// // Licensed under the MIT license.
// // See LICENSE.txt file in the project root for full license information.

using System;
using System.Collections.Generic;
using SharpYaml.Model;

namespace SharpYaml.Serialization;

/// <summary>
/// Describes a YAML mapping key that could not be mapped to a member of the CLR type being deserialized.
/// </summary>
/// <remarks>
/// Instances are passed to <see cref="YamlSerializerOptions.UnmappedMemberCallback"/> and
/// <see cref="YamlSerializerOptions.UnmappedMembersFinalizer"/>.
/// </remarks>
public sealed class YamlUnmappedMember
{
    private readonly string[] _knownMemberNames;
    private object? _capturedValue;
    private YamlElement? _value;
    private bool _valueResolved;
    private string? _suggestedName;
    private bool _suggestionResolved;

    internal YamlUnmappedMember(
        string? sourceName,
        Type declaringType,
        object? instance,
        string name,
        string path,
        Mark keyStart,
        Mark valueStart,
        string[] knownMemberNames)
    {
        SourceName = sourceName;
        DeclaringType = declaringType;
        Instance = instance;
        Name = name;
        Path = path;
        KeyStart = keyStart;
        ValueStart = valueStart;
        _knownMemberNames = knownMemberNames;
    }

    /// <summary>Gets the source name (for example a file path) from <see cref="YamlSerializerOptions.SourceName"/>.</summary>
    public string? SourceName { get; }

    /// <summary>Gets the CLR type that was being deserialized when the key was encountered.</summary>
    public Type DeclaringType { get; }

    /// <summary>
    /// Gets the instance being populated, or <see langword="null"/> when it does not exist yet
    /// (for example for types deserialized through a parameterized constructor).
    /// </summary>
    public object? Instance { get; }

    /// <summary>Gets the unmapped YAML key.</summary>
    public string Name { get; }

    /// <summary>
    /// Gets the path of the member from the document root, for example <c>$.servers[2].tls.unknownKey</c>.
    /// Keys that are not simple identifiers are written as <c>$['a.b']</c>.
    /// </summary>
    /// <remarks>
    /// For members inside polymorphic nodes the path is complete, but <see cref="KeyStart"/> and
    /// <see cref="ValueStart"/> are relative to the buffered node rather than the document.
    /// </remarks>
    public string Path { get; }

    /// <summary>Gets the location of the key.</summary>
    public Mark KeyStart { get; }

    /// <summary>Gets the location where the value starts.</summary>
    public Mark ValueStart { get; }

    /// <summary>
    /// Gets a value indicating whether the member was stored in a <see cref="YamlExtensionDataAttribute"/> member
    /// (<see langword="true"/>) or skipped (<see langword="false"/>).
    /// </summary>
    public bool IsCapturedByExtensionData { get; private set; }

    /// <summary>Gets the serialized names of the members that <see cref="DeclaringType"/> accepts.</summary>
    public IReadOnlyList<string> KnownMemberNames => _knownMemberNames;

    /// <summary>Gets the value of the member as a YAML element.</summary>
    /// <remarks>For members captured by extension data, the element is created on first access.</remarks>
    public YamlElement? Value
    {
        get
        {
            if (!_valueResolved)
            {
                var captured = _capturedValue;
                _capturedValue = null;
                _value = captured switch
                {
                    null => null,
                    YamlElement element => element,
                    _ => YamlElement.FromObject(captured),
                };
                _valueResolved = true;
            }

            return _value;
        }
    }

    /// <summary>
    /// Gets the closest name in <see cref="KnownMemberNames"/> (ignoring case) when it is a likely misspelling of
    /// <see cref="Name"/>; otherwise <see langword="null"/>.
    /// </summary>
    public string? SuggestedName
    {
        get
        {
            if (!_suggestionResolved)
            {
                _suggestedName = FindSuggestion(Name, _knownMemberNames);
                _suggestionResolved = true;
            }

            return _suggestedName;
        }
    }

    internal void SetValue(YamlElement? value)
    {
        _value = value;
        _valueResolved = true;
    }

    internal void SetCapturedValue(object? value)
    {
        IsCapturedByExtensionData = true;
        _capturedValue = value;
    }

    private static string? FindSuggestion(string name, string[] candidates)
    {
        var maxDistance = Math.Max(1, name.Length / 3);
        string? best = null;
        var bestDistance = int.MaxValue;
        for (var i = 0; i < candidates.Length; i++)
        {
            var candidate = candidates[i];
            if (Math.Abs(candidate.Length - name.Length) > maxDistance)
            {
                continue;
            }

            var distance = GetDistance(name, candidate);
            if (distance <= maxDistance && distance < bestDistance)
            {
                best = candidate;
                bestDistance = distance;
            }
        }

        return best;
    }

    // Optimal string alignment distance (Levenshtein plus adjacent transpositions), ignoring case.
    private static int GetDistance(string a, string b)
    {
        var width = b.Length + 1;
        var size = width * 3;
        Span<int> rows = size <= 256 ? stackalloc int[size] : new int[size];
        var previous2 = rows.Slice(0, width);
        var previous = rows.Slice(width, width);
        var current = rows.Slice(width * 2, width);

        for (var j = 0; j < width; j++)
        {
            previous[j] = j;
        }

        for (var i = 1; i <= a.Length; i++)
        {
            current[0] = i;
            var ca = char.ToUpperInvariant(a[i - 1]);
            for (var j = 1; j <= b.Length; j++)
            {
                var cb = char.ToUpperInvariant(b[j - 1]);
                var cost = ca == cb ? 0 : 1;
                var value = Math.Min(Math.Min(current[j - 1] + 1, previous[j] + 1), previous[j - 1] + cost);
                if (i > 1 && j > 1 && ca == char.ToUpperInvariant(b[j - 2]) && char.ToUpperInvariant(a[i - 2]) == cb)
                {
                    value = Math.Min(value, previous2[j - 2] + 1);
                }

                current[j] = value;
            }

            var rotated = previous2;
            previous2 = previous;
            previous = current;
            current = rotated;
        }

        return previous[b.Length];
    }
}
