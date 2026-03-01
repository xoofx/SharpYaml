// // Copyright (c) Alexandre Mutel. All rights reserved.
// // Licensed under the MIT license.
// // See LICENSE.txt file in the project root for full license information.

using System;

namespace SharpYaml;

internal class StringLookAheadBuffer : ILookAheadBuffer
{
    private readonly string value;

    public int Length { get { return value.Length; } }

    public int Position { get; private set; }

    private bool IsOutside(int index)
    {
        return index >= value.Length;
    }

    public bool EndOfInput { get { return IsOutside(Position); } }

    public StringLookAheadBuffer(string value)
    {
        this.value = value;
    }

    public char Peek(int offset)
    {
        int index = Position + offset;
        return IsOutside(index) ? '\0' : value[index];
    }

    public void Skip(int length)
    {
        if (length < 0)
        {
            throw new ArgumentOutOfRangeException("length", "The length must be positive.");
        }
        Position += length;
    }

    public void Cache(int length) { }
}