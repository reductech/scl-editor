﻿using Reductech.Sequence.Core.LanguageServer.Objects;

namespace Reductech.Utilities.SCLEditor.Util.Objects;

public record VSSignatureResponse(
    IReadOnlyList<VSSignatureHelpItem> Signatures,
    int ActiveSignature,
    int ActiveParameter)
{
    public VSSignatureResponse(SignatureHelpResponse response)
        : this(
            response.Signatures.Select(x => new VSSignatureHelpItem(x)).ToList(),
            response.ActiveSignature,
            response.ActiveParameter
        ) { }

    public static VSSignatureResponse Empty { get; } =
        new(ArraySegment<VSSignatureHelpItem>.Empty, 0, 0);
}
