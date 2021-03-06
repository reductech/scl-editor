using Reductech.Sequence.Core.LanguageServer.Objects;

namespace Reductech.Utilities.SCLEditor.Components.Objects;

public record VSCompletionResponse(List<VSCompletionItem> Suggestions, bool IsIncomplete)
{
    public VSCompletionResponse(CompletionResponse r) : this(
        r.Items.Select(x => new VSCompletionItem(x)).ToList(),
        r.IsIncomplete
    ) { }
}
