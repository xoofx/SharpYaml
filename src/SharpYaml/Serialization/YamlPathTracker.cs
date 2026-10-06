// // Copyright (c) Alexandre Mutel. All rights reserved.
// // Licensed under the MIT license.
// // See LICENSE.txt file in the project root for full license information.

using System.Text;

namespace SharpYaml.Serialization;

/// <summary>
/// Tracks the document path of the reader by observing its tokens. Only created when unmapped member reporting is enabled.
/// </summary>
/// <remarks>
/// Completion of a value is applied lazily on the next token so that, while the reader is positioned on a value,
/// the path still includes the key of that value. Complex (non-scalar) mapping keys are not tracked accurately.
/// </remarks>
internal sealed class YamlPathTracker
{
    private enum MappingState : byte
    {
        Key,
        Value,
        ValueDone,
    }

    private struct Frame
    {
        public bool IsMapping;
        public MappingState State;
        public string? Key;
        public Mark KeyStart;
        public int Index;
    }

    private readonly string? _prefix;
    private Frame[] _frames = new Frame[8];
    private int _count;

    public YamlPathTracker(YamlUnmappedMemberSession session, string? prefix)
    {
        Session = session;
        _prefix = prefix;
    }

    /// <summary>Gets the session shared by this tracker's reader and the readers created for buffered nodes.</summary>
    public YamlUnmappedMemberSession Session { get; }

    public void OnToken(YamlTokenType tokenType, string? scalarValue, Mark start)
    {
        switch (tokenType)
        {
            case YamlTokenType.Scalar:
            case YamlTokenType.Alias:
                if (_count == 0)
                {
                    return;
                }

                ref var top = ref _frames[_count - 1];
                if (!top.IsMapping)
                {
                    top.Index++;
                    return;
                }

                if (top.State == MappingState.ValueDone)
                {
                    top.State = MappingState.Key;
                }

                if (top.State == MappingState.Key)
                {
                    top.Key = scalarValue ?? "?";
                    top.KeyStart = start;
                    top.State = MappingState.Value;
                }
                else
                {
                    top.State = MappingState.ValueDone;
                }

                return;

            case YamlTokenType.StartMapping:
            case YamlTokenType.StartSequence:
                EnterValue(start);
                Push(isMapping: tokenType == YamlTokenType.StartMapping);
                return;

            case YamlTokenType.EndMapping:
            case YamlTokenType.EndSequence:
                if (_count == 0)
                {
                    return;
                }

                _count--;
                if (_count > 0 && _frames[_count - 1].IsMapping)
                {
                    _frames[_count - 1].State = MappingState.ValueDone;
                }

                return;
        }
    }

    public string GetPath()
    {
        var builder = new StringBuilder(_prefix ?? "$");
        for (var i = 0; i < _count; i++)
        {
            ref var frame = ref _frames[i];
            if (frame.IsMapping)
            {
                if (frame.State != MappingState.Key)
                {
                    AppendKey(builder, frame.Key ?? "?");
                }
            }
            else if (frame.Index >= 0)
            {
                builder.Append('[').Append(frame.Index).Append(']');
            }
        }

        return builder.ToString();
    }

    /// <summary>Gets the location of the key that owns the value the reader is currently positioned on.</summary>
    public Mark GetCurrentKeyStart()
    {
        for (var i = _count - 1; i >= 0; i--)
        {
            ref var frame = ref _frames[i];
            if (frame.IsMapping && frame.State != MappingState.Key)
            {
                return frame.KeyStart;
            }
        }

        return Mark.Empty;
    }

    private void EnterValue(Mark start)
    {
        if (_count == 0)
        {
            return;
        }

        ref var top = ref _frames[_count - 1];
        if (!top.IsMapping)
        {
            top.Index++;
            return;
        }

        if (top.State == MappingState.ValueDone)
        {
            top.State = MappingState.Key;
        }

        if (top.State == MappingState.Key)
        {
            // Complex key: not tracked precisely, but keep the state machine total.
            top.Key = "?";
            top.KeyStart = start;
            top.State = MappingState.Value;
        }
    }

    private void Push(bool isMapping)
    {
        if (_count == _frames.Length)
        {
            System.Array.Resize(ref _frames, _count * 2);
        }

        _frames[_count++] = new Frame { IsMapping = isMapping, Index = -1 };
    }

    private static void AppendKey(StringBuilder builder, string key)
    {
        if (IsSimpleKey(key))
        {
            builder.Append('.').Append(key);
            return;
        }

        builder.Append("['").Append(key.Replace("'", "\\'")).Append("']");
    }

    private static bool IsSimpleKey(string key)
    {
        if (key.Length == 0)
        {
            return false;
        }

        foreach (var c in key)
        {
            if (!(char.IsLetterOrDigit(c) || c == '_' || c == '-'))
            {
                return false;
            }
        }

        return true;
    }
}
