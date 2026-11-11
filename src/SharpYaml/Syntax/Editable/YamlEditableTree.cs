// // Copyright (c) Alexandre Mutel. All rights reserved.
// // Licensed under the MIT license.
// // See LICENSE.txt file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace SharpYaml.Syntax.Editable;

/// <summary>
/// An editable YAML document: a <see cref="YamlSyntaxTree"/> grouped into a mutable tree of
/// maps, lists and scalars with attached comments, in the spirit of Tomlyn.Syntax.DocumentSyntax.
/// Scope is intentionally bounded to editing hand-written configuration files: block-style maps
/// and sequences, plain/quoted scalars, "#" comments, keys and list items without a value
/// ("key:", "-"), and empty flow collections ("[]", "{}"), the only form of an empty map or list.
/// Flow style with content, anchors, aliases and tags are not supported and cause
/// <see cref="Parse"/> to throw <see cref="NotSupportedException"/>.
/// </summary>
/// <remarks>
/// Edits are applied to the source snapshot through <see cref="YamlSyntaxTree.WithTextChange"/>
/// (see <see cref="ToSyntaxTree"/>), so the result is validated by the regular parser and every
/// character outside the edited range is retained.
/// </remarks>
public sealed class YamlEditableTree
{
    private YamlEditableTree(YamlSyntaxTree syntaxTree, YamlEditableNode root, List<YamlEditableTrivia>? trailingTrivia)
    {
        SyntaxTree = syntaxTree;
        Root = root;
        TrailingTrivia = trailingTrivia;
    }

    /// <summary>
    /// Gets the source snapshot this tree was parsed from. It does not change when the tree is edited.
    /// </summary>
    public YamlSyntaxTree SyntaxTree { get; }

    /// <summary>
    /// Gets the root node (a <see cref="YamlEditableMap"/> for the common case of a document
    /// whose top level is a mapping).
    /// </summary>
    public YamlEditableNode Root { get; }

    /// <summary>
    /// Gets trivia (typically the final newline, or trailing comments/blank lines) that follows
    /// the last significant token of the document. Might be <see langword="null"/>.
    /// </summary>
    public List<YamlEditableTrivia>? TrailingTrivia { get; }

    /// <summary>
    /// Parses YAML text into an editable tree.
    /// </summary>
    /// <exception cref="NotSupportedException">
    /// The document uses a construct outside the bounded scope of this editable tree (flow
    /// style with content, anchors, aliases, tags, or multiple documents).
    /// </exception>
    public static YamlEditableTree Parse(string yaml)
    {
        ArgumentGuard.ThrowIfNull(yaml);

        var syntaxTree = YamlSyntaxTree.Parse(yaml);
        var builder = new Builder(syntaxTree.Tokens);
        var root = builder.BuildRoot();
        return new YamlEditableTree(syntaxTree, root, builder.TrailingTrivia);
    }

    /// <summary>
    /// Applies the edits made to this tree to <see cref="SyntaxTree"/> as a single
    /// <see cref="YamlSyntaxTree.WithTextChange"/> over the smallest range that differs, and
    /// returns the new, validated syntax tree. If the tree was not edited, returns
    /// <see cref="SyntaxTree"/>.
    /// </summary>
    /// <returns>A syntax tree whose text equals <see cref="Serialize"/>.</returns>
    /// <exception cref="YamlException">The edited text is not valid YAML.</exception>
    public YamlSyntaxTree ToSyntaxTree()
    {
        var oldText = SyntaxTree.Text;
        var newText = Serialize();
        if (string.Equals(oldText, newText, StringComparison.Ordinal))
        {
            return SyntaxTree;
        }

        var prefix = 0;
        var maxPrefix = Math.Min(oldText.Length, newText.Length);
        while (prefix < maxPrefix && oldText[prefix] == newText[prefix])
        {
            prefix++;
        }

        var suffix = 0;
        var maxSuffix = maxPrefix - prefix;
        while (suffix < maxSuffix && oldText[oldText.Length - 1 - suffix] == newText[newText.Length - 1 - suffix])
        {
            suffix++;
        }

        return SyntaxTree.WithTextChange(
            prefix,
            oldText.Length - prefix - suffix,
            newText.Substring(prefix, newText.Length - prefix - suffix));
    }

    /// <summary>
    /// Serializes the tree back to YAML text. Parts of the document that were not edited
    /// reproduce their original text exactly, including comments and formatting.
    /// </summary>
    public string Serialize()
    {
        using var writer = new StringWriter();
        WriteTo(writer);
        return writer.ToString();
    }

    /// <summary>
    /// Writes the tree back to YAML text.
    /// </summary>
    public void WriteTo(TextWriter writer)
    {
        ArgumentGuard.ThrowIfNull(writer);
        Root.WriteTo(writer);
        if (TrailingTrivia != null)
        {
            foreach (var trivia in TrailingTrivia)
            {
                writer.Write(trivia.Text);
            }
        }
    }

    /// <inheritdoc cref="Serialize" />
    public override string ToString() => Serialize();

    /// <summary>
    /// Builds a <see cref="YamlEditableNode"/> tree from the flat, position-sorted token stream
    /// produced by <see cref="YamlSyntaxTree.Parse"/>.
    ///
    /// The stream is NOT a clean, strictly-nested sequence: zero-width structural markers
    /// (<see cref="YamlSyntaxKind.Key"/>, <see cref="YamlSyntaxKind.BlockMappingStart"/>,
    /// <see cref="YamlSyntaxKind.BlockEnd"/>) can appear in either order at the same source
    /// position (e.g. "Key" before or after "BlockMappingStart" depending on parser lookahead),
    /// and trivia is scanned independently of scalar boundaries, so a quoted scalar spanning a
    /// newline produces a spurious NewLineTrivia token nested inside the scalar's own span. This
    /// builder therefore does not assume a fixed token order: it treats the zero-width markers as
    /// structural noise consumed opportunistically, and derives shape from the tokens that do
    /// carry real text (Scalar, Value ":", BlockEntry "-", BlockSequenceStart) together with
    /// indentation recovered from trivia.
    /// </summary>
    private sealed class Builder
    {
        private readonly List<YamlSyntaxToken> _significant;

        // Trivia before a significant token that is on the same line as the token: the whole
        // lines above it (comments, blank lines) and its indentation. StartsLine is true when a
        // line break comes before the token (or the token starts the document).
        private readonly Dictionary<int, (List<YamlEditableTrivia>? Trivia, bool StartsLine)> _leadingTriviaByTokenIndex;

        // Trivia after a significant token up to and including the first line break: a
        // same-line comment and the newline that ends the token's line.
        private readonly Dictionary<int, List<YamlEditableTrivia>> _endOfLineByTokenIndex;
        private int _position;

        /// <summary>
        /// Trivia left over after the last significant token (e.g. the document's final
        /// newline), captured because it never becomes another token's leading trivia.
        /// </summary>
        public List<YamlEditableTrivia>? TrailingTrivia { get; private set; }

        public Builder(IReadOnlyList<YamlSyntaxToken> tokens)
        {
            _significant = [];
            _leadingTriviaByTokenIndex = [];
            _endOfLineByTokenIndex = [];

            var pendingTrivia = new List<YamlEditableTrivia>();
            var containingScalarEnd = -1;
            var openFlowCollection = false;
            foreach (var token in tokens)
            {
                if (IsTrivia(token.Kind))
                {
                    if (token.Span.Start.Index < containingScalarEnd)
                    {
                        // Trivia scanned independently of scalar boundaries can fall inside a
                        // quoted/block scalar that already contains this text verbatim (e.g. a
                        // newline inside a double-quoted scalar). Skip it: the owning Scalar
                        // token's Text already carries it.
                        continue;
                    }

                    pendingTrivia.Add(ToTrivia(token));
                    continue;
                }

                if (token.Kind is YamlSyntaxKind.StreamStart or YamlSyntaxKind.StreamEnd
                    or YamlSyntaxKind.DocumentStart or YamlSyntaxKind.DocumentEnd
                    or YamlSyntaxKind.Key or YamlSyntaxKind.BlockMappingStart or YamlSyntaxKind.BlockEnd)
                {
                    // Zero-width structural noise: not represented as nodes in the editable tree.
                    // Map/list nesting is instead recovered from indentation (see IndentOf) and
                    // from Value/BlockEntry/BlockSequenceStart, which do carry reliable shape.
                    // Their position relative to neighboring trivia is not fixed (see class
                    // remarks), so they are dropped rather than consumed in an assumed order.
                    continue;
                }

                if (openFlowCollection)
                {
                    // Only an empty flow collection is supported: its end must follow its start at once.
                    var start = _significant[_significant.Count - 1];
                    var closes = pendingTrivia.Count == 0
                        && (token.Kind, start.Kind) is (YamlSyntaxKind.FlowSequenceEnd, YamlSyntaxKind.FlowSequenceStart)
                            or (YamlSyntaxKind.FlowMappingEnd, YamlSyntaxKind.FlowMappingStart);
                    if (!closes)
                    {
                        throw UnsupportedFlowCollection(start.Kind);
                    }

                    // The start token stands for the whole "[]" or "{}".
                    openFlowCollection = false;
                    continue;
                }

                if (token.Kind is YamlSyntaxKind.FlowSequenceStart or YamlSyntaxKind.FlowMappingStart)
                {
                    openFlowCollection = true;
                }
                else if (IsUnsupported(token.Kind))
                {
                    throw new NotSupportedException(
                        $"YamlEditableTree does not support '{token.Kind}'. Only block maps, block " +
                        "sequences, empty flow collections (\"[]\", \"{}\"), scalars and comments are supported.");
                }

                AttachTrivia(pendingTrivia, _significant.Count);
                pendingTrivia = [];

                _significant.Add(token);
                if (token.Kind == YamlSyntaxKind.Scalar)
                {
                    containingScalarEnd = Math.Max(containingScalarEnd, token.Span.End.Index);
                }
            }

            if (openFlowCollection)
            {
                throw UnsupportedFlowCollection(_significant[_significant.Count - 1].Kind);
            }

            if (pendingTrivia.Count > 0)
            {
                var rest = SplitEndOfLine(pendingTrivia, _significant.Count);
                TrailingTrivia = rest.Count > 0 ? rest : null;
            }
        }

        private static NotSupportedException UnsupportedFlowCollection(YamlSyntaxKind start) => new(
            $"YamlEditableTree does not support '{start}' with content. Only empty flow collections " +
            "(\"[]\", \"{}\") are supported.");

        /// <summary>
        /// Attaches the trivia found before the significant token at <paramref name="nextIndex"/>:
        /// the part up to the first line break ends the previous token's line, the rest is the
        /// leading trivia of the next token.
        /// </summary>
        private void AttachTrivia(List<YamlEditableTrivia> trivia, int nextIndex)
        {
            if (nextIndex == 0)
            {
                // The start of the document is the start of a line.
                if (trivia.Count > 0)
                {
                    _leadingTriviaByTokenIndex[0] = (trivia, true);
                }

                return;
            }

            var startsLine = trivia.Exists(t => t.Kind == YamlEditableTriviaKind.NewLine);
            var rest = SplitEndOfLine(trivia, nextIndex);
            if (rest.Count > 0 || startsLine)
            {
                _leadingTriviaByTokenIndex[nextIndex] = (rest.Count > 0 ? rest : null, startsLine);
            }
        }

        /// <summary>
        /// Stores the trivia up to and including the first line break as the end of line of the
        /// token before <paramref name="nextIndex"/>, and returns the rest.
        /// </summary>
        private List<YamlEditableTrivia> SplitEndOfLine(List<YamlEditableTrivia> trivia, int nextIndex)
        {
            var newLine = trivia.FindIndex(t => t.Kind == YamlEditableTriviaKind.NewLine);
            if (newLine < 0 || nextIndex == 0)
            {
                return trivia;
            }

            _endOfLineByTokenIndex[nextIndex - 1] = trivia.GetRange(0, newLine + 1);
            return trivia.GetRange(newLine + 1, trivia.Count - newLine - 1);
        }

        public YamlEditableNode BuildRoot()
        {
            if (_significant.Count == 0)
            {
                return new YamlEditableMap();
            }

            var node = ParseValue(string.Empty);
            if (_position != _significant.Count)
            {
                throw new NotSupportedException(
                    "YamlEditableTree encountered unexpected trailing content while parsing.");
            }

            // A scalar document ("value # comment\n") has no entry to own its end of line.
            if (TakeEndOfLine(_significant.Count - 1) is { } endOfLine)
            {
                endOfLine.AddRange(TrailingTrivia ?? []);
                TrailingTrivia = endOfLine;
            }

            if (_endOfLineByTokenIndex.Count > 0 || _leadingTriviaByTokenIndex.Count > 0)
            {
                // Every piece of trivia must be owned by a node, or the text would not round-trip.
                throw new NotSupportedException(
                    "YamlEditableTree encountered trivia in an unsupported position while parsing.");
            }

            return node;
        }

        /// <summary>
        /// Parses the value that starts at the current position, which is either a "Value" or
        /// "BlockEntry" marker's payload, or (only at the document root) the very first token.
        /// Looks ahead to decide whether it is a nested map (next significant token is a
        /// "Value" at greater or equal indentation than <paramref name="parentIndent"/>), a
        /// nested list ("BlockSequenceStart"), or a scalar.
        /// </summary>
        private YamlEditableNode ParseValue(string parentIndent)
        {
            // A sequence at the column of its key ("key:\n- a") has no BlockSequenceStart: it starts
            // directly with its first BlockEntry.
            if (Current.Kind is YamlSyntaxKind.BlockSequenceStart or YamlSyntaxKind.BlockEntry)
            {
                return ParseList(parentIndent);
            }

            if (Current.Kind == YamlSyntaxKind.FlowSequenceStart)
            {
                Take();
                return new YamlEditableList();
            }

            if (Current.Kind == YamlSyntaxKind.FlowMappingStart)
            {
                Take();
                return new YamlEditableMap();
            }

            if (Current.Kind == YamlSyntaxKind.Scalar && IsMapKeyAhead())
            {
                return ParseMap(parentIndent);
            }

            if (Current.Kind == YamlSyntaxKind.Scalar)
            {
                var token = Take();
                return new YamlEditableScalar(token.Text);
            }

            throw new NotSupportedException(
                $"YamlEditableTree encountered an unexpected token '{Current.Kind}' while parsing.");
        }

        /// <summary>
        /// A "Scalar" starts a nested map (rather than being a leaf value) when it is
        /// immediately followed by a "Value" token, i.e. it is itself a key.
        /// </summary>
        private bool IsMapKeyAhead() =>
            _position + 1 < _significant.Count && _significant[_position + 1].Kind == YamlSyntaxKind.Value;

        private YamlEditableMap ParseMap(string parentIndent)
        {
            var map = new YamlEditableMap();
            // null = "not yet committed": true until a key with an EXPLICIT indent (its own
            // newline) is seen. A key with no explicit indent shares its line with whatever
            // introduces this map (e.g. "- Name: x" — "Name" follows "-" on the same line, no
            // newline before it, so SplitIndent returns null and its indent is only a fallback,
            // not a real measurement). Committing mapIndent from that fallback would wrongly cut
            // the map at the very next key, whose real indent (its own line, e.g. "  Port: y"
            // aligned under "Name") is almost always different from the fallback. So an
            // uncommitted mapIndent accepts any indent without ending the map; only once a real
            // (explicit) indent is seen does mapIndent lock in and start rejecting mismatches.
            string? mapIndent = null;
            var mapIndentCommitted = false;

            // Every key of a mapping is at the column of its first key. The indentation text alone
            // does not tell it for the first key of a list item, which shares its line with "-": a
            // key at another column (e.g. "B" in "S:\n  - x: 1\nB: 3") belongs to an outer mapping.
            var firstKeyColumn = -1;

            while (_position < _significant.Count && Current.Kind == YamlSyntaxKind.Scalar && IsMapKeyAhead())
            {
                if (firstKeyColumn < 0)
                {
                    firstKeyColumn = Current.Span.Start.Column;
                }
                else if (Current.Span.Start.Column != firstKeyColumn)
                {
                    break;
                }

                var rawLeading = TakeLeadingTrivia();
                var (indentOrNull, keyLeading) = SplitIndent(rawLeading);
                var indent = indentOrNull ?? parentIndent;

                if (mapIndentCommitted && indent != mapIndent)
                {
                    // A dedent back to (or below) the parent level ends this mapping; the caller
                    // continues parsing at its own level from the current position. Restore the
                    // trivia exactly as read, so the caller's own SplitIndent sees the full run.
                    UndoLeadingTrivia(rawLeading);
                    break;
                }

                if (indentOrNull is not null)
                {
                    mapIndent = indent;
                    mapIndentCommitted = true;
                }

                var keyScalarToken = Take();
                var valueToken = Expect(YamlSyntaxKind.Value);

                YamlEditableNode valueNode;
                List<YamlEditableTrivia>? separatorTrivia;
                List<YamlEditableTrivia>? endOfLine;
                if (IsValueMissing(valueToken, keyScalarToken.Span.Start.Column, sequenceAtOwnerColumnIsValue: true))
                {
                    // "key:" with nothing after it: the line ends after the ":".
                    valueNode = new YamlEditableScalar(string.Empty);
                    separatorTrivia = null;
                    endOfLine = TakeEndOfLine(_position - 1);
                }
                else
                {
                    // When the value is a nested list or map on its own line, the trivia right after
                    // "Value" (a newline plus the nested block's own indentation) is what the nested
                    // ParseList/ParseMap needs as the leading trivia of its own first entry, not a
                    // separator owned by this MapEntry — leave it in place instead of taking it here.
                    var nestedBlockAhead = Current.Kind is YamlSyntaxKind.BlockSequenceStart or YamlSyntaxKind.BlockEntry
                        || (Current.Kind == YamlSyntaxKind.Scalar && IsMapKeyAhead());
                    // For a nested block, the separator is only the end of the "key:" line: the
                    // nested entries keep their own indentation.
                    separatorTrivia = nestedBlockAhead ? TakeEndOfLine(_position - 1) : TakeSeparator();
                    valueNode = ParseValue(indent);
                    endOfLine = YamlEditableLayout.IsBlock(valueNode) ? null : TakeEndOfLine(_position - 1);
                }

                // A key with no explicit indent (indentOrNull is null) shares its line with the
                // construct that introduces this map (e.g. the "-" of a YamlEditableListEntry,
                // whose own WriteCore already writes "- "). WriteCore below unconditionally
                // writes Entry.Indent before the key, so this entry's stored Indent must be ""
                // in that case — using the inherited "indent" (parentIndent) here would duplicate
                // the whitespace already written by the parent on reserialize (confirmed by a
                // round-trip test: "- Name: x" became "-   Name: x", invalid YAML once the extra
                // indent stacked with the ListEntry's own separator).
                var entryIndent = indentOrNull ?? string.Empty;
                var entry = new YamlEditableMapEntry(keyScalarToken.Text, valueNode, entryIndent)
                {
                    LeadingTrivia = keyLeading,
                    TrailingTrivia = separatorTrivia,
                    EndOfLineTrivia = endOfLine,
                };
                map.AddParsedEntry(entry);
            }

            return map;
        }

        private YamlEditableList ParseList(string parentIndent)
        {
            // The whitespace before "BlockSequenceStart" (a zero-width marker sharing the first
            // entry's position) carries the list's indentation; capture it before Expect
            // consumes the marker, since no trivia is separately attached to the first
            // BlockEntry itself.
            var firstEntryLeadingRaw = TakeLeadingTrivia();
            if (Current.Kind == YamlSyntaxKind.BlockSequenceStart)
            {
                Take();
            }
            var list = new YamlEditableList();
            var isFirstEntry = true;

            while (_position < _significant.Count && Current.Kind == YamlSyntaxKind.BlockEntry)
            {
                var rawLeading = isFirstEntry ? firstEntryLeadingRaw : TakeLeadingTrivia();
                isFirstEntry = false;
                var (indentOrNull, entryLeading) = SplitIndent(rawLeading);
                var indent = indentOrNull ?? parentIndent;
                var dashToken = Take();

                YamlEditableNode itemNode;
                List<YamlEditableTrivia>? itemLeading;
                List<YamlEditableTrivia>? endOfLine;
                if (IsValueMissing(dashToken, dashToken.Span.Start.Column, sequenceAtOwnerColumnIsValue: false))
                {
                    // "-" with nothing after it: the line ends after the "-".
                    itemNode = new YamlEditableScalar(string.Empty);
                    itemLeading = null;
                    endOfLine = TakeEndOfLine(_position - 1);
                }
                else
                {
                    itemLeading = TakeSeparator();
                    itemNode = ParseValue(indent);
                    endOfLine = YamlEditableLayout.IsBlock(itemNode) ? null : TakeEndOfLine(_position - 1);
                }

                var entry = new YamlEditableListEntry(itemNode, indent)
                {
                    LeadingTrivia = entryLeading,
                    TrailingTrivia = itemLeading,
                    EndOfLineTrivia = endOfLine,
                };
                list.AddParsedEntry(entry);
            }

            return list;
        }

        /// <summary>
        /// Returns <see langword="true"/> if the "key:" or "-" <paramref name="marker"/> just taken has no
        /// value: the document ends, or the next token starts a later line at a column that cannot belong
        /// to the value. The scanner emits no token for an empty value, so without this check the next
        /// key would be read as a nested map. A nested block is indented past <paramref name="ownerColumn"/>
        /// (the column of the key or the "-"); a sequence may also start at the key's own column
        /// ("key:\n- a"), as YAML allows.
        /// </summary>
        private bool IsValueMissing(YamlSyntaxToken marker, int ownerColumn, bool sequenceAtOwnerColumnIsValue)
        {
            if (_position >= _significant.Count)
            {
                return true;
            }

            var next = Current.Span.Start;
            if (next.Line == marker.Span.Start.Line)
            {
                return false;
            }

            var isSequence = Current.Kind is YamlSyntaxKind.BlockSequenceStart or YamlSyntaxKind.BlockEntry;
            return sequenceAtOwnerColumnIsValue && isSequence ? next.Column < ownerColumn : next.Column <= ownerColumn;
        }

        private static bool IsTrivia(YamlSyntaxKind kind) =>
            kind is YamlSyntaxKind.WhitespaceTrivia or YamlSyntaxKind.NewLineTrivia or YamlSyntaxKind.CommentTrivia;

        private static bool IsUnsupported(YamlSyntaxKind kind) =>
            kind is YamlSyntaxKind.FlowSequenceStart or YamlSyntaxKind.FlowSequenceEnd
                or YamlSyntaxKind.FlowMappingStart or YamlSyntaxKind.FlowMappingEnd
                or YamlSyntaxKind.FlowEntry or YamlSyntaxKind.Anchor or YamlSyntaxKind.AnchorAlias
                or YamlSyntaxKind.Tag or YamlSyntaxKind.VersionDirective or YamlSyntaxKind.TagDirective;

        private static YamlEditableTrivia ToTrivia(YamlSyntaxToken token) => new(
            token.Kind switch
            {
                YamlSyntaxKind.WhitespaceTrivia => YamlEditableTriviaKind.Whitespace,
                YamlSyntaxKind.NewLineTrivia => YamlEditableTriviaKind.NewLine,
                _ => YamlEditableTriviaKind.Comment,
            },
            token.Text);

        /// <summary>
        /// Splits leading trivia into (a) the indentation string of the token it precedes: the
        /// run of whitespace immediately before it, after the last newline; and (b) the
        /// remaining trivia with that trailing indentation run removed, suitable for a node's
        /// <see cref="YamlEditableNode.LeadingTrivia"/> — indentation is instead reproduced by
        /// the owning <see cref="YamlEditableMapEntry.Indent"/>/<see cref="YamlEditableListEntry.Indent"/>
        /// in <c>WriteCore</c>, so writing both would duplicate it. Returns
        /// (<see langword="null"/>, the trivia unchanged) if the token does not start a line
        /// (e.g. a key on the same line as "-" has no indentation of its own).
        /// </summary>
        private static (string? Indent, List<YamlEditableTrivia>? Trivia) SplitIndent((List<YamlEditableTrivia>? Trivia, bool StartsLine) leading)
        {
            var leadingTrivia = leading.Trivia;
            if (!leading.StartsLine)
            {
                return (null, leadingTrivia);
            }

            if (leadingTrivia is null || leadingTrivia.Count == 0)
            {
                return (string.Empty, null);
            }

            var lastNewLine = leadingTrivia.FindLastIndex(t => t.Kind == YamlEditableTriviaKind.NewLine);
            var indentRunStart = lastNewLine + 1;
            if (indentRunStart >= leadingTrivia.Count || leadingTrivia[indentRunStart].Kind != YamlEditableTriviaKind.Whitespace)
            {
                // No whitespace run after the last newline: zero indentation, nothing to trim.
                return (string.Empty, leadingTrivia);
            }

            var indent = leadingTrivia[indentRunStart].Text;
            var remaining = leadingTrivia.Take(indentRunStart).ToList();
            return (indent, remaining.Count > 0 ? remaining : null);
        }

        private YamlSyntaxToken Current => _significant[_position];

        private YamlSyntaxToken Take()
        {
            var token = Current;
            _position++;
            return token;
        }

        private YamlSyntaxToken Expect(YamlSyntaxKind kind)
        {
            var token = Current;
            if (token.Kind != kind)
            {
                throw new NotSupportedException(
                    $"YamlEditableTree expected '{kind}' but found '{token.Kind}'.");
            }

            _position++;
            return token;
        }

        private (List<YamlEditableTrivia>? Trivia, bool StartsLine) TakeLeadingTrivia()
        {
            if (!_leadingTriviaByTokenIndex.TryGetValue(_position, out var leading))
            {
                return (null, false);
            }

            _leadingTriviaByTokenIndex.Remove(_position);
            return leading;
        }

        private void UndoLeadingTrivia((List<YamlEditableTrivia>? Trivia, bool StartsLine) leading)
        {
            if (leading.Trivia != null || leading.StartsLine)
            {
                _leadingTriviaByTokenIndex[_position] = leading;
            }
        }

        private List<YamlEditableTrivia>? TakeEndOfLine(int tokenIndex)
        {
            if (!_endOfLineByTokenIndex.TryGetValue(tokenIndex, out var trivia))
            {
                return null;
            }

            _endOfLineByTokenIndex.Remove(tokenIndex);
            return trivia;
        }

        /// <summary>
        /// Takes all the trivia between the previous token and the current one: the end of the
        /// previous token's line, if any, and the leading trivia of the current token.
        /// </summary>
        private List<YamlEditableTrivia>? TakeSeparator()
        {
            var endOfLine = TakeEndOfLine(_position - 1);
            var leading = TakeLeadingTrivia().Trivia;
            if (endOfLine is null)
            {
                return leading;
            }

            if (leading is not null)
            {
                endOfLine.AddRange(leading);
            }

            return endOfLine;
        }
    }
}
