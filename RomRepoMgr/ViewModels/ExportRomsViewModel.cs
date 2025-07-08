/******************************************************************************
// RomRepoMgr - ROM repository manager
// ----------------------------------------------------------------------------
//
// Author(s)      : Natalia Portillo <claunia@claunia.com>
//
// --[ License ] --------------------------------------------------------------
//
//     This program is free software: you can redistribute it and/or modify
//     it under the terms of the GNU General Public License as
//     published by the Free Software Foundation, either version 3 of the
//     License, or (at your option) any later version.
//
//     This program is distributed in the hope that it will be useful,
//     but WITHOUT ANY WARRANTY; without even the implied warranty of
//     MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
//     GNU General Public License for more details.
//
//     You should have received a copy of the GNU General Public License
//     along with this program.  If not, see <http://www.gnu.org/licenses/>.
//
// ----------------------------------------------------------------------------
// Copyright © 2020-2024 Natalia Portillo
*******************************************************************************/

using System;
using System.Reactive;
using System.Threading.Tasks;
using Avalonia.Threading;
using ReactiveUI;
using RomRepoMgr.Core.EventArgs;
using RomRepoMgr.Core.Workers;
using RomRepoMgr.Views;

namespace RomRepoMgr.ViewModels;

public sealed class ExportRomsViewModel : ViewModelBase
{
    readonly long       _romSetId;
    readonly ExportRoms _view;
    bool                _canClose;
    bool                _progress2IsIndeterminate;
    double              _progress2Maximum;
    double              _progress2Minimum;
    double              _progress2Value;
    bool                _progress2Visible;
    bool                _progress3IsIndeterminate;
    double              _progress3Maximum;
    double              _progress3Minimum;
    double              _progress3Value;
    bool                _progress3Visible;
    bool                _progressIsIndeterminate;
    double              _progressMaximum;
    double              _progressMinimum;
    double              _progressValue;
    bool                _progressVisible;
    string              _status2Message;
    string              _status3Message;
    string              _statusMessage;

    public ExportRomsViewModel(ExportRoms view, string folderPath, long romSetId)
    {
        _view        = view;
        _romSetId    = romSetId;
        FolderPath   = folderPath;
        CloseCommand = ReactiveCommand.Create(ExecuteCloseCommand);
        CanClose     = false;
    }

    public string FolderPath { get; }

    public bool ProgressVisible
    {
        get => _progressVisible;
        set => this.RaiseAndSetIfChanged(ref _progressVisible, value);
    }

    public string StatusMessage
    {
        get => _statusMessage;
        set => this.RaiseAndSetIfChanged(ref _statusMessage, value);
    }

    public double ProgressMinimum
    {
        get => _progressMinimum;
        set => this.RaiseAndSetIfChanged(ref _progressMinimum, value);
    }

    public double ProgressMaximum
    {
        get => _progressMaximum;
        set => this.RaiseAndSetIfChanged(ref _progressMaximum, value);
    }

    public double ProgressValue
    {
        get => _progressValue;
        set => this.RaiseAndSetIfChanged(ref _progressValue, value);
    }

    public bool ProgressIsIndeterminate
    {
        get => _progressIsIndeterminate;
        set => this.RaiseAndSetIfChanged(ref _progressIsIndeterminate, value);
    }

    public bool Progress2Visible
    {
        get => _progress2Visible;
        set => this.RaiseAndSetIfChanged(ref _progress2Visible, value);
    }

    public string Status2Message
    {
        get => _status2Message;
        set => this.RaiseAndSetIfChanged(ref _status2Message, value);
    }

    public double Progress2Minimum
    {
        get => _progress2Minimum;
        set => this.RaiseAndSetIfChanged(ref _progress2Minimum, value);
    }

    public double Progress2Maximum
    {
        get => _progress2Maximum;
        set => this.RaiseAndSetIfChanged(ref _progress2Maximum, value);
    }

    public double Progress2Value
    {
        get => _progress2Value;
        set => this.RaiseAndSetIfChanged(ref _progress2Value, value);
    }

    public bool Progress2IsIndeterminate
    {
        get => _progress2IsIndeterminate;
        set => this.RaiseAndSetIfChanged(ref _progress2IsIndeterminate, value);
    }

    public bool Progress3Visible
    {
        get => _progress3Visible;
        set => this.RaiseAndSetIfChanged(ref _progress3Visible, value);
    }

    public string Status3Message
    {
        get => _status3Message;
        set => this.RaiseAndSetIfChanged(ref _status3Message, value);
    }

    public double Progress3Minimum
    {
        get => _progress3Minimum;
        set => this.RaiseAndSetIfChanged(ref _progress3Minimum, value);
    }

    public double Progress3Maximum
    {
        get => _progress3Maximum;
        set => this.RaiseAndSetIfChanged(ref _progress3Maximum, value);
    }

    public double Progress3Value
    {
        get => _progress3Value;
        set => this.RaiseAndSetIfChanged(ref _progress3Value, value);
    }

    public bool Progress3IsIndeterminate
    {
        get => _progress3IsIndeterminate;
        set => this.RaiseAndSetIfChanged(ref _progress3IsIndeterminate, value);
    }

    public bool CanClose
    {
        get => _canClose;
        set => this.RaiseAndSetIfChanged(ref _canClose, value);
    }

    public ReactiveCommand<Unit, Unit> CloseCommand { get; }

    void ExecuteCloseCommand() => _view.Close();

    void OnWorkerOnFinished(object sender, EventArgs args) => Dispatcher.UIThread.Post(() =>
    {
        ProgressVisible  = false;
        CanClose         = true;
        Progress2Visible = false;
        Progress3Visible = false;
    });

    void OnWorkerOnSetProgressBounds(object sender, ProgressBoundsEventArgs args) => Dispatcher.UIThread.Post(() =>
    {
        ProgressIsIndeterminate = false;
        ProgressMaximum         = args.Maximum;
        ProgressMinimum         = args.Minimum;
    });

    void OnWorkerOnSetProgress(object sender, ProgressEventArgs args) =>
        Dispatcher.UIThread.Post(() => ProgressValue = args.Value);

    void OnWorkerOnSetMessage(object sender, MessageEventArgs args) =>
        Dispatcher.UIThread.Post(() => StatusMessage = args.Message);

    void OnWorkerOnSetIndeterminateProgress(object sender, EventArgs args) =>
        Dispatcher.UIThread.Post(() => ProgressIsIndeterminate = true);

    void OnWorkerOnSetProgressBounds2(object sender, ProgressBoundsEventArgs args) => Dispatcher.UIThread.Post(() =>
    {
        Progress2Visible         = true;
        Progress2IsIndeterminate = false;
        Progress2Maximum         = args.Maximum;
        Progress2Minimum         = args.Minimum;
    });

    void OnWorkerOnSetProgress2(object sender, ProgressEventArgs args) =>
        Dispatcher.UIThread.Post(() => Progress2Value = args.Value);

    void OnWorkerOnSetMessage2(object sender, MessageEventArgs args) =>
        Dispatcher.UIThread.Post(() => Status2Message = args.Message);

    void OnWorkerOnSetProgressBounds3(object sender, ProgressBoundsEventArgs args) => Dispatcher.UIThread.Post(() =>
    {
        Progress3Visible         = true;
        Progress3IsIndeterminate = false;
        Progress3Maximum         = args.Maximum;
        Progress3Minimum         = args.Minimum;
    });

    void OnWorkerOnSetProgress3(object sender, ProgressEventArgs args) =>
        Dispatcher.UIThread.Post(() => Progress3Value = args.Value);

    void OnWorkerOnSetMessage3(object sender, MessageEventArgs args) =>
        Dispatcher.UIThread.Post(() => Status3Message = args.Message);

    public void OnOpened()
    {
        var worker = new FileExporter(_romSetId, FolderPath);
        worker.SetMessage         += OnWorkerOnSetMessage;
        worker.SetProgress        += OnWorkerOnSetProgress;
        worker.SetProgressBounds  += OnWorkerOnSetProgressBounds;
        worker.SetMessage2        += OnWorkerOnSetMessage2;
        worker.SetProgress2       += OnWorkerOnSetProgress2;
        worker.SetProgress2Bounds += OnWorkerOnSetProgressBounds2;
        worker.SetMessage3        += OnWorkerOnSetMessage3;
        worker.SetProgress3       += OnWorkerOnSetProgress3;
        worker.SetProgress3Bounds += OnWorkerOnSetProgressBounds3;
        worker.WorkFinished       += OnWorkerOnFinished;

        ProgressVisible = true;

        _ = Task.Run(worker.Export);
    }
}