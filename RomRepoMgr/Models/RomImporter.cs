using System;
using System.Threading.Tasks;
using Avalonia.Media;
using ReactiveUI;
using RomRepoMgr.Core.EventArgs;

namespace RomRepoMgr.Models;

public class RomImporter : ReactiveObject
{
    bool          _indeterminate;
    double        _maximum;
    double        _minimum;
    double        _progress;
    Color         _statusColor;
    string        _statusMessage;
    public string Filename { get; internal init; }
    public bool   Running  { get; private set; } = true;

    public bool Indeterminate
    {
        get => _indeterminate;
        set => this.RaiseAndSetIfChanged(ref _indeterminate, value);
    }

    public double Progress
    {
        get => _progress;
        set => this.RaiseAndSetIfChanged(ref _progress, value);
    }

    public double Maximum
    {
        get => _maximum;
        set => this.RaiseAndSetIfChanged(ref _maximum, value);
    }

    public double Minimum
    {
        get => _minimum;
        set => this.RaiseAndSetIfChanged(ref _minimum, value);
    }

    public string StatusMessage
    {
        get => _statusMessage;
        set => this.RaiseAndSetIfChanged(ref _statusMessage, value);
    }

    public Color StatusColor
    {
        get => _statusColor;
        set => this.RaiseAndSetIfChanged(ref _statusColor, value);
    }

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

    public void OnImportedRom(object sender, ImportedRomItemEventArgs e)
    {
        Indeterminate = false;
        Maximum       = 1;
        Minimum       = 0;
        Progress      = 1;
        StatusMessage = e.Item.Status;
        Running       = false;
    }
}