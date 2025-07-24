using System;
using System.Threading.Tasks;
using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using RomRepoMgr.Core.EventArgs;

namespace RomRepoMgr.Models;

public partial class DatImporter : ObservableObject
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
    Color _statusColor;
    [ObservableProperty]
    string _statusMessage;
    public string Filename { get; internal init; }
    public Task   Task     { get; set; }
    public bool   Running  { get; private set; } = true;

    internal void OnErrorOccurred(object sender, ErrorEventArgs e)
    {
        StatusMessage = e.Message;
        StatusColor   = Colors.Red;

        if(!Indeterminate) return;

        Indeterminate = false;
        Progress      = 0;
    }

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
        Indeterminate = false;
        Maximum       = 1;
        Minimum       = 0;
        Progress      = 1;
        StatusMessage = e.Message;
        Running       = false;
    }
}