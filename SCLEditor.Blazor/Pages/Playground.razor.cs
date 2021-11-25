﻿using MudBlazor;

namespace Reductech.Utilities.SCLEditor.Blazor.Pages;

/// <summary>
/// Playground for using SCL
/// </summary>
public partial class Playground
{
    /// <summary>
    /// The JS runtime
    /// </summary>
    [Inject]
    public IJSRuntime Runtime { get; set; } = null!;

    [Inject] public IDialogService DialogService { get; set; }

    [CascadingParameter] public CompoundFileSystem FileSystem { get; set; }

    /// <summary>
    /// The _scl editor to use
    /// </summary>
    private MonacoEditor _sclEditor = null!;

    private MonacoEditor? _fileEditor = null!;

    bool OutputExpanded { get; set; } = true;
    bool LogExpanded { get; set; } = true;

    private readonly ITestLoggerFactory _testLoggerFactory =
        TestLoggerFactory.Create(x => x.FilterByMinimumLevel(LogLevel.Information));

    private readonly ICompression _compression = new CompressionAdapter();

    private readonly StringBuilder _consoleStringBuilder = new();

    private StepFactoryStore _stepFactoryStore = null!;

    private FileSelection _fileSelection;

    private CancellationTokenSource? RunCancellation { get; set; }

    private SCLCodeHelper _sclCodeHelper = null!;

    private EditorConfiguration _configuration = new();

    private FileData? _openedFile = null;

    private bool _hotChanges = false;
    MudMessageBox MudMessageBox { get; set; }

    private string? _title = null;

    /// <inheritdoc />
    protected override async Task OnInitializedAsync()
    {
        await base.OnInitializedAsync();

        Console.SetOut(new StringWriter(_consoleStringBuilder));

        var stepFactoryStoreResult = StepFactoryStore.TryCreateFromAssemblies(
            ExternalContext.Default,
            typeof(FileRead).Assembly,
            typeof(ToCSV).Assembly
        );

        _stepFactoryStore = stepFactoryStoreResult.Value;
    }

    /// <inheritdoc />
    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender)
        {
            var containsConfigKey =
                await FileSystem.LocalStorage.ContainKeyAsync(EditorConfiguration.ConfigurationKey);

            if (containsConfigKey)
                _configuration = await
                    FileSystem.LocalStorage.GetItemAsync<EditorConfiguration>(
                        EditorConfiguration.ConfigurationKey
                    );
            else
                _configuration = new EditorConfiguration();

            _configuration.PropertyChanged += _configuration_PropertyChanged;

            _sclCodeHelper = new SCLCodeHelper(_stepFactoryStore, _configuration);

            var objRef = DotNetObjectReference.Create(_sclCodeHelper);

            await Runtime.InvokeVoidAsync(
                "registerSCL",
                objRef
            ); //Function Defined in DefineSCLLanguage.js

            var model = await _sclEditor.GetModel();
            await MonacoEditorBase.SetModelLanguage(model, "scl");

            await _sclEditor.AddAction(
                "runSCL",
                "Run SCL",
                new[] { (int)KeyMode.CtrlCmd | (int)KeyCode.KEY_R },
                null,
                null,
                "SCL",
                1.5,
                async (_, _) =>
                {
                    await Run();
                    StateHasChanged();
                }
            );

            await _sclEditor.AddAction(
                "formatscl",
                "Format SCL",
                new[] { (int)KeyMode.CtrlCmd | (int)KeyCode.KEY_F },
                null,
                null,
                "SCL",
                1.5,
                async (_, _) =>
                {
                    await FormatSCL();
                }
            );
        }

        await base.OnAfterRenderAsync(firstRender);
    }

    private async Task SaveSCLFile()
    {
        if (_title is null)
        {
            var change = await MudMessageBox.Show(new DialogOptions() { });

            if (change != true)
                return;

            if (_title is null)
                return;
        }

        if (!_title.EndsWith(".scl", StringComparison.InvariantCultureIgnoreCase))
        {
            _title += ".scl";
        }

        _hotChanges = false;

        await _fileSelection.FileSystem.SaveFile(_sclEditor, _title);
    }

    private async Task CloseOpenFile()
    {
        _openedFile = null;
    }

    private async Task SaveOpenFile()
    {
        if (_fileEditor is not null && _openedFile is not null)
        {
            await FileSystem.SaveFile(_fileEditor, _openedFile.Path);
        }
    }

    private async Task OpenFileAction(FileData arg)
    {
        if (Path.GetExtension(arg.Path) == ".scl")
        {
            _title      = arg.Path;
            _hotChanges = false;
            await _sclEditor.SetValue(arg.Data.TextContents);
        }
        else
        {
            await CloseOpenFile();

            _openedFile = arg;

            if (_fileEditor is not null)
                await _fileEditor.SetValue(arg.Data.TextContents);

            StateHasChanged();
        }
    }

    private async Task SaveConfiguration()
    {
        await FileSystem.LocalStorage.SetItemAsync(
            EditorConfiguration.ConfigurationKey,
            _configuration
        );
    }

    private void _configuration_PropertyChanged(
        object? sender,
        PropertyChangedEventArgs e)
    {
        SaveConfiguration();
    }

    private void ClearLogs()
    {
        _testLoggerFactory.Sink.Clear();
    }

    private string LogText()
    {
        var text =
            string.Join(
                "\r\n",
                _testLoggerFactory.Sink.LogEntries.Select(x => x.Message)
            );

        return text;
    }

    private Task OnDidChangeModelContentAsync()
    {
        _hotChanges = true;
        return _sclCodeHelper.SetDiagnostics(_sclEditor, Runtime);
    }

    private void CancelRun()
    {
        RunCancellation?.Cancel();
        RunCancellation = null;
    }

    private async Task FormatSCL()
    {
        var sclText = await _sclEditor.GetValue();

        var selections = await _sclEditor.GetSelections();

        var uri = (await _sclEditor.GetModel()).Uri;

        var edits = Formatter
            .FormatDocument(sclText, _stepFactoryStore)
            .ToList();

        await _sclEditor.ExecuteEdits(uri, edits, selections);
    }

    private async Task Run()
    {
        var sclText = await _sclEditor.GetValue();

        RunCancellation?.Cancel();
        var cts = new CancellationTokenSource();
        RunCancellation = cts;

        var logger = _testLoggerFactory.CreateLogger("SCL");

        var stepResult = SCLParsing.TryParseStep(sclText)
            .Bind(x => x.TryFreeze(SCLRunner.RootCallerMetadata, _stepFactoryStore));

        if (stepResult.IsFailure)
        {
            _consoleStringBuilder.AppendLine(stepResult.Error.AsString);
        }
        else
        {
            var externalContext = new ExternalContext(
                ExternalProcessRunner.Instance,
                DefaultRestClientFactory.Instance,
                ConsoleAdapter.Instance,
                (ConnectorInjection.FileSystemKey, FileSystem.FileSystem),
                (ConnectorInjection.CompressionKey, _compression)
            );

            await using var stateMonad = new StateMonad(
                logger,
                _stepFactoryStore,
                externalContext,
                new Dictionary<string, object>()
            );

            var runResult = await stepResult.Value.Run<object>(
                stateMonad,
                RunCancellation.Token
            );

            if (runResult.IsFailure)
                _consoleStringBuilder.AppendLine(runResult.Error.AsString);

            else if (runResult.Value is Unit)
                _consoleStringBuilder.AppendLine("Sequence Completed Successfully");
            else
            {
                _consoleStringBuilder.AppendLine(runResult.Value.ToString());
            }
        }

        RunCancellation = null;

        _consoleStringBuilder.AppendLine();
    }

    private static StandaloneEditorConstructionOptions SCLEditorConstructionOptions(MonacoEditor _)
    {
        return new()
        {
            AutomaticLayout = true,
            Language        = "scl",
            Value = @"- print 123
- log 456"
        };
    }

    private StandaloneEditorConstructionOptions GetFileEditorConstructionOptions(FileData file)
    {
        var extension =
            GetLanguageFromFileExtension(FileSystem.FileSystem.Path.GetExtension(file.Path));

        return new()
        {
            AutomaticLayout = true,
            Language        = extension,
            Value           = file.Data.TextContents,
            WordWrap        = "off",
            TabSize         = 8,
            UseTabStops     = true,
        };

        static string GetLanguageFromFileExtension(string extension)
        {
            return extension?.ToLowerInvariant() switch

            {
                "yml"  => "yaml",
                "yaml" => "yaml",
                "json" => "json",
                "cs"   => "csharp",
                _      => ""
            };
        }
    }
}