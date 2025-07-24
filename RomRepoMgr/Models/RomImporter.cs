using System;
using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using RomRepoMgr.Core.EventArgs;

namespace RomRepoMgr.Models;

public partial class RomImporter : ObservableObject
{
    [ObservableProperty]
    bool _indeterminate;
    [ObservableProperty]
    double _maximum;
    [ObservableProperty]
    double _minimum;
    [ObservableProperty]
    double _progress;
    [ObservableProperty]
    bool _progressVisible = true;
    [ObservableProperty]
    Color _statusColor;
    [ObservableProperty]
    string _statusMessage;
    public string Filename { get; internal init; }
    public bool   Running  { get; private set; } = true;

    internal void OnSetIndeterminateProgress(object sender, EventArgs e)
    {
        Indeterminate = true;
    }

    internal void OnSetMessage(object sender, MessageEventArgs e)
    {
        StatusMessage = e.Message;
    }

    internal void OnSetProgress(object sender, ProgressEventArgs e)
    {
        Progress = e.Value;
    }

    internal void OnSetProgressBounds(object sender, ProgressBoundsEventArgs e)
    {
        Indeterminate = false;
        Maximum       = e.Maximum;
        Minimum       = e.Minimum;
    }

    internal void OnWorkFinished(object sender, MessageEventArgs e)
    {
        Indeterminate   = false;
        Maximum         = 1;
        Minimum         = 0;
        Progress        = 1;
        StatusMessage   = e.Message;
        Running         = false;
        ProgressVisible = false;
    }

    public void OnImportedRom(object sender, ImportedRomItemEventArgs e)
    {
        Indeterminate   = false;
        Maximum         = 1;
        Minimum         = 0;
        Progress        = 1;
        StatusMessage   = e.Item.Status;
        Running         = false;
        ProgressVisible = false;
    }
}