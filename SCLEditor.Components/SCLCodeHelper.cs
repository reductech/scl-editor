using Reductech.Sequence.Core.Internal;
using Reductech.Sequence.Core.Internal.Documentation;
using Reductech.Sequence.Core.LanguageServer;
using Reductech.Utilities.SCLEditor.Components.Objects;

namespace Reductech.Utilities.SCLEditor.Components;

/// <summary>
/// Contains JS Invokable methods for working with SCL
/// </summary>
public class SCLCodeHelper
{
    /// <summary>
    /// Create a new SCL Code Helper
    /// </summary>
    public SCLCodeHelper(StepFactoryStore stepFactoryStore, EditorConfiguration configuration)
    {
        StepFactoryStore = stepFactoryStore;
        Configuration    = configuration;
    }

    /// <summary>
    /// The Step factory store
    /// </summary>
    public StepFactoryStore StepFactoryStore { get; }

    /// <summary>
    /// The editor configuration
    /// </summary>
    public EditorConfiguration Configuration { get; }

    /// <summary>
    /// Gets code completion from a completion request
    /// </summary>
    [JSInvokable]
    public async Task<VSCompletionResponse> GetCompletionAsync(
        string code,
        VSCompletionRequest vsCompletionRequest)
    {
        await Task.CompletedTask;

        if (!Configuration.CompletionEnabled)
            return new VSCompletionResponse(new List<VSCompletionItem>(), true);

        if (vsCompletionRequest.Position is null)
        {
            return new VSCompletionResponse(new List<VSCompletionItem>(), true);
        }

        var position = vsCompletionRequest.Position.AsLinePosition();

        var result = CompletionHelper.GetCompletionResponse(
            code,
            position,
            StepFactoryStore,
            DocumentationOptions.DefaultDocumentationOptionsMonaco
        );

        return new(result);
    }

    /// <summary>
    /// Resolve a completion request
    /// </summary>
    [JSInvokable]
    public async Task<VSCompletionResolveResponse> GetCompletionResolveAsync(
        VSCompletionResolveRequest vsCompletionResolveRequest)
    {
        await Task.CompletedTask;
        return new VSCompletionResolveResponse { Item = vsCompletionResolveRequest.Item };
    }

    /// <summary>
    /// Gets signature help
    /// </summary>
    [JSInvokable]
    public async Task<VSSignatureResponse> GetSignatureHelpAsync(
        string code,
        VSSignatureHelpRequest vsSignatureHelpRequest)
    {
        await Task.CompletedTask;

        if (!Configuration.SignatureHelpEnabled)
            return VSSignatureResponse.Empty;

        //if (vsSignatureHelpRequest.Position is null)
        //    return VSSignatureResponse.Empty;

        var response = SignatureHelpHelper.GetSignatureHelpResponse(
            code,
            vsSignatureHelpRequest.Position.AsLinePosition(),
            StepFactoryStore,
            DocumentationOptions.DefaultDocumentationOptionsMonaco
        );

        return new(response);
    }

    /// <summary>
    /// Gets quick info (hover)
    /// </summary>
    [JSInvokable]
    public async Task<VSQuickInfoResponse> GetQuickInfoAsync(
        string code,
        VSQuickInfoRequest vsQuickInfoRequest)
    {
        await Task.CompletedTask;

        if (!Configuration.QuickInfoEnabled)
            return new VSQuickInfoResponse(ArraySegment<VSString>.Empty);

        var result = QuickInfoHelper.GetQuickInfoAsync(
            code,
            vsQuickInfoRequest.Position.AsLinePosition(),
            StepFactoryStore
        );

        return new(result);
    }
}
