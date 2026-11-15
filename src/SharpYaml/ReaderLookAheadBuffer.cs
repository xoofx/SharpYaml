// // Copyright (c) Alexandre Mutel. All rights reserved.
// // Licensed under the MIT license.
// // See LICENSE.txt file in the project root for full license information.

using System.IO;

namespace SharpYaml;

/// <summary>
/// The buffer of a reader that the scanner reads itself: <see cref="Scanner{TBuffer}"/> takes <see cref="Reader"/> and
/// reads it into a window it scans in place, instead of asking this buffer for one character at a time.
/// </summary>
/// <remarks>
/// The scanner only asks the reader for more when it needs a character it does not have, and asks a reader that may
/// wait for its input for no more than that, so a document is still parsed as soon as it arrived from a pipe or a
/// socket whose writer stays open.
/// </remarks>
internal sealed class ReaderLookAheadBuffer : LookAheadBuffer
{
    /// <param name="reader">The reader of the YAML.</param>
    /// <param name="initialWindowSize">The size of the first window, or 0 for the default; tests use a small one to move the window often.</param>
    public ReaderLookAheadBuffer(TextReader reader, int initialWindowSize = 0)
        : base(reader, Scanner<LookAheadBuffer>.MaxBufferLength)
    {
        Reader = reader;
        InitialWindowSize = initialWindowSize;
    }

    /// <summary>Gets the reader of the YAML.</summary>
    public TextReader Reader { get; }

    /// <summary>Gets the size of the first window, or 0 for the default.</summary>
    public int InitialWindowSize { get; }
}
