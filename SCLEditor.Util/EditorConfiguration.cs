﻿namespace Reductech.Utilities.SCLEditor.Util;

/// <summary>
/// Configuration for the SCL editor
/// </summary>
public class EditorConfiguration : INotifyPropertyChanged
{
    /// <summary>
    /// The configuration key in local storage
    /// </summary>
    public const string ConfigurationKey = "SCLPlaygroundConfiguration";

    private bool _completionEnabled = true;

    /// <summary>
    /// Whether code completion is enabled
    /// </summary>
    public bool CompletionEnabled
    {
        get => _completionEnabled;
        set
        {
            if (value == _completionEnabled)
                return;

            _completionEnabled = value;
            OnPropertyChanged();
        }
    }

    private bool _signatureHelpEnabled = true;

    /// <summary>
    /// Whether Signature help is enabled
    /// </summary>
    public bool SignatureHelpEnabled
    {
        get => _signatureHelpEnabled;
        set
        {
            if (value == _signatureHelpEnabled)
                return;

            _signatureHelpEnabled = value;
            OnPropertyChanged();
        }
    }

    private bool _quickInfoEnabled = true;

    /// <summary>
    /// Whether quick info is enabled
    /// </summary>
    public bool QuickInfoEnabled
    {
        get => _quickInfoEnabled;
        set
        {
            if (value == _quickInfoEnabled)
                return;

            _quickInfoEnabled = value;
            OnPropertyChanged();
        }
    }

    private bool _diagnosticsEnabled = false;

    /// <summary>
    /// Whether diagnostics is enabled
    /// </summary>
    public bool DiagnosticsEnabled
    {
        get => _diagnosticsEnabled;
        set
        {
            if (value == _diagnosticsEnabled)
                return;

            _diagnosticsEnabled = value;
            OnPropertyChanged();
        }
    }

    private bool _minimapEnabled = false;

    /// <summary>
    /// Whether the minimap view is enabled
    /// </summary>
    public bool MinimapEnabled
    {
        get => _minimapEnabled;
        set
        {
            if (value == _minimapEnabled)
                return;

            _minimapEnabled = value;
            OnPropertyChanged((nameof(MinimapEnabled)));
        }
    }

    /// <summary>
    /// Called whenever a configuration property is changed
    /// </summary>
    public event PropertyChangedEventHandler? PropertyChanged;

    [NotifyPropertyChangedInvocator]
    protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
