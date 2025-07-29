using System;
using Avalonia.Media;
using Avalonia.Threading;
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

    internal void OnErrorOccurred(object sender, ErrorEventArgs e) => Dispatcher.UIThread.Post(() =>
    {
        StatusMessage = e.Message;
        StatusColor   = Colors.Red;

        if(!Indeterminate) return;

        Indeterminate = false;
        Progress      = 0;
    });

    internal void OnSetIndeterminateProgress(object sender, EventArgs e) =>
        Dispatcher.UIThread.Post(() => Indeterminate = true);

    internal void OnSetMessage(object sender, MessageEventArgs e) =>
        Dispatcher.UIThread.Post(() => StatusMessage = e.Message);

    internal void OnSetProgress(object sender, ProgressEventArgs e) =>
        Dispatcher.UIThread.Post(() => Progress = e.Value);

    internal void OnSetProgressBounds(object sender, ProgressBoundsEventArgs e) => Dispatcher.UIThread.Post(() =>
    {
        Indeterminate = false;
        Maximum       = e.Maximum;
        Minimum       = e.Minimum;
    });

    internal void OnWorkFinished(object sender, MessageEventArgs e) => Dispatcher.UIThread.Post(() =>
    {
        Indeterminate = false;
        Maximum       = 1;
        Minimum       = 0;
        Progress      = 1;
        StatusMessage = e.Message;
    });
}