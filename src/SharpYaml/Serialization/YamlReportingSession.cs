// // Copyright (c) Alexandre Mutel. All rights reserved.
// // Licensed under the MIT license.
// // See LICENSE.txt file in the project root for full license information.

using System;
using System.Collections.Generic;

namespace SharpYaml.Serialization;

/// <summary>
/// Per-deserialization reporting state. Created only when a report is consumed, and shared between a reader and the
/// readers it creates for buffered (polymorphic) nodes.
/// </summary>
internal sealed class YamlReportingSession
{
    private readonly Action<YamlUnmappedMember>? _callback;
    private readonly Action<IReadOnlyList<YamlUnmappedMember>>? _finalizer;
    private readonly Action<YamlInvalidValue>? _invalidValueCallback;
    private readonly Action<IReadOnlyList<YamlInvalidValue>>? _invalidValuesFinalizer;
    private List<YamlUnmappedMember>? _members;
    private List<YamlInvalidValue>? _invalidValues;

    private YamlReportingSession(YamlSerializerOptions options)
    {
        _callback = options.UnmappedMemberCallback;
        _finalizer = options.UnmappedMembersFinalizer;
        _invalidValueCallback = options.InvalidValueCallback;
        _invalidValuesFinalizer = options.InvalidValuesFinalizer;
        InvalidValueHandling = options.InvalidValueHandling;
    }

    /// <summary>Whether an unmapped member callback or finalizer is configured.</summary>
    public bool IsReportingUnmappedMembers => _callback is not null || _finalizer is not null;

    /// <summary>Whether an invalid value callback or finalizer is configured, that is whether a skipped value is worth capturing.</summary>
    public bool IsReportingInvalidValues => _invalidValueCallback is not null || _invalidValuesFinalizer is not null;

    /// <summary>The configured handling of invalid values.</summary>
    public YamlInvalidValueHandling InvalidValueHandling { get; }

    /// <summary>Anchors defined by the nodes reported so far, so that later reported nodes can alias them.</summary>
    public Dictionary<string, Model.YamlElement> Anchors { get; } = new();

    /// <summary>The path of the node most recently buffered by the reader; used to seed the path of the re-parse.</summary>
    public string? BufferedPath { get; set; }

    /// <summary>
    /// Creates a session when a report is consumed: an unmapped member delegate is set, invalid values fail at the end,
    /// or they are skipped and a delegate is set. Otherwise returns <see langword="null"/> so that the default costs nothing.
    /// </summary>
    public static YamlReportingSession? Create(YamlSerializerOptions options)
    {
        var reportsUnmappedMembers = options.UnmappedMemberCallback is not null || options.UnmappedMembersFinalizer is not null;
        var reportsInvalidValues = options.InvalidValueHandling switch
        {
            YamlInvalidValueHandling.FailAtEnd => true,
            YamlInvalidValueHandling.Skip => options.InvalidValueCallback is not null || options.InvalidValuesFinalizer is not null,
            _ => false,
        };

        return reportsUnmappedMembers || reportsInvalidValues ? new YamlReportingSession(options) : null;
    }

    public void Report(YamlUnmappedMember member)
    {
        _callback?.Invoke(member);
        if (_finalizer is not null)
        {
            (_members ??= new List<YamlUnmappedMember>()).Add(member);
        }
    }

    public void Report(YamlInvalidValue value)
    {
        _invalidValueCallback?.Invoke(value);
        if (_invalidValuesFinalizer is not null || InvalidValueHandling == YamlInvalidValueHandling.FailAtEnd)
        {
            (_invalidValues ??= new List<YamlInvalidValue>()).Add(value);
        }
    }

    /// <summary>Whether invalid values were recovered that make the deserialization fail at its end.</summary>
    public bool FailsAtEnd => InvalidValueHandling == YamlInvalidValueHandling.FailAtEnd && _invalidValues is { Count: > 0 };

    /// <summary>
    /// Creates the exception for a deserialization that ended with <paramref name="fatal"/> after at least one value was
    /// recovered under <see cref="YamlInvalidValueHandling.FailAtEnd"/>. Finalizers do not run, because the read did not complete.
    /// </summary>
    public YamlInvalidValuesException CreateException(YamlException fatal)
    {
        var invalidValues = _invalidValues ?? throw new InvalidOperationException("No invalid value was recovered.");
        _invalidValues = null;
        return new YamlInvalidValuesException(invalidValues, fatal);
    }

    /// <summary>
    /// Runs the finalizers once the read completed, then throws when invalid values were recovered under
    /// <see cref="YamlInvalidValueHandling.FailAtEnd"/>.
    /// </summary>
    /// <exception cref="YamlInvalidValuesException">At least one value was recovered and the handling is <see cref="YamlInvalidValueHandling.FailAtEnd"/>.</exception>
    public void Complete()
    {
        var members = _members;
        if (_finalizer is not null && members is { Count: > 0 })
        {
            _members = null;
            _finalizer(members);
        }

        var invalidValues = _invalidValues;
        if (invalidValues is not { Count: > 0 })
        {
            return;
        }

        _invalidValues = null;
        _invalidValuesFinalizer?.Invoke(invalidValues);
        if (InvalidValueHandling == YamlInvalidValueHandling.FailAtEnd)
        {
            throw new YamlInvalidValuesException(invalidValues);
        }
    }
}
