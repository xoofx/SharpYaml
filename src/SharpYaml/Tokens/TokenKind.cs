// // Copyright (c) Alexandre Mutel. All rights reserved.
// // Licensed under the MIT license.
// // See LICENSE.txt file in the project root for full license information.

namespace SharpYaml.Tokens;

/// <summary>
/// The kind of a <see cref="Token"/>, set by the constructor of each token class: the parser tests it instead of the
/// type of the token, which costs a call for every token class that is not sealed.
/// </summary>
internal enum TokenKind : byte
{
    None,
    Anchor,
    AnchorAlias,
    BlockEnd,
    BlockEntry,
    BlockMappingStart,
    BlockSequenceStart,
    DocumentEnd,
    DocumentStart,
    FlowEntry,
    FlowMappingEnd,
    FlowMappingStart,
    FlowSequenceEnd,
    FlowSequenceStart,
    Key,
    ReservedDirective,
    Scalar,
    StreamEnd,
    StreamStart,
    Tag,
    TagDirective,
    Value,
    VersionDirective,
}
