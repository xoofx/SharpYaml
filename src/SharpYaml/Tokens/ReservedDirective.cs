// Copyright (c) Alexandre Mutel. All rights reserved.
// Licensed under the MIT license.

namespace SharpYaml.Tokens;

// Retain reserved directives until document boundaries have been validated.
internal sealed class ReservedDirective : Token
{
    public ReservedDirective(Mark start, Mark end) : base(start, end)
    {
    }
}
