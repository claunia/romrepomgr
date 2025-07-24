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
using System.Threading.Tasks;
using System.Windows.Input;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using RomRepoMgr.Core.EventArgs;
using RomRepoMgr.Core.Workers;
using RomRepoMgr.Views;

namespace RomRepoMgr.ViewModels;

public sealed partial class ExportRomsViewModel : ViewModelBase
{
    readonly long       _romSetId;
    readonly ExportRoms _view;
    [ObservableProperty]
    bool _canClose;
    [ObservableProperty]
    bool _progress2IsIndeterminate;
    [ObservableProperty]
    double _progress2Maximum;
    [ObservableProperty]
    double _progress2Minimum;
    [ObservableProperty]
    double _progress2Value;
    [ObservableProperty]
    bool _progress2Visible;
    [ObservableProperty]
    bool _progress3IsIndeterminate;
    [ObservableProperty]
    double _progress3Maximum;
    [ObservableProperty]
    double _progress3Minimum;
    [ObservableProperty]
    double _progress3Value;
    [ObservableProperty]
    bool _progress3Visible;
    [ObservableProperty]
    bool _progressIsIndeterminate;
    [ObservableProperty]
    double _progressMaximum;
    [ObservableProperty]
    double _progressMinimum;
    [ObservableProperty]
    double _progressValue;
    [ObservableProperty]
    bool _progressVisible;
    [ObservableProperty]
    string _status2Message;
    [ObservableProperty]
    string _status3Message;
    [ObservableProperty]
    string _statusMessage;

    // Mock
    public ExportRomsViewModel()
    {
#pragma warning disable PH2080
        FolderPath = "C:\\ExportedRoms";
#pragma warning restore PH2080
    }

    public ExportRomsViewModel(ExportRoms view, string folderPath, long romSetId)
    {
        _view        = view;
        _romSetId    = romSetId;
        FolderPath   = folderPath;
        CloseCommand = new RelayCommand(ExecuteCloseCommand);
        CanClose     = false;
    }

    public string   FolderPath   { get; }
    public ICommand CloseCommand { get; }

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