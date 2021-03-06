using Reductech.Sequence.Core.LanguageServer.Objects;

namespace Reductech.Utilities.SCLEditor.Components.Objects;

public record VSSignatureParameter(string Label, string Name, VSString Documentation)
{
    public VSSignatureParameter(SignatureHelpParameter parameter) : this(
        parameter.Label,
        parameter.Name,
        new VSString(parameter.Documentation)
    ) { }
}
