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

public sealed partial class ImportDatViewModel : ViewModelBase
{
    readonly ImportDat   _view;
    readonly DatImporter _worker;
    [ObservableProperty]
    bool _canClose;
    [ObservableProperty]
    double _currentValue;
    [ObservableProperty]
    string _errorMessage;
    [ObservableProperty]
    bool _errorVisible;
    [ObservableProperty]
    bool _indeterminateProgress;
    [ObservableProperty]
    double _maximumValue;
    [ObservableProperty]
    double _minimumValue;
    [ObservableProperty]
    bool _progressVisible;
    [ObservableProperty]
    string _statusMessage;

    // Mock
    public ImportDatViewModel() {}

    public ImportDatViewModel(ImportDat view, string datPath)
    {
        _view                            =  view;
        CloseCommand                     =  new RelayCommand(ExecuteCloseCommand);
        IndeterminateProgress            =  true;
        ProgressVisible                  =  false;
        ErrorVisible                     =  false;
        _worker                          =  new DatImporter(datPath, null);
        _worker.ErrorOccurred            += OnWorkerOnErrorOccurred;
        _worker.SetIndeterminateProgress += OnWorkerOnSetIndeterminateProgress;
        _worker.SetMessage               += OnWorkerOnSetMessage;
        _worker.SetProgress              += OnWorkerOnSetProgress;
        _worker.SetProgressBounds        += OnWorkerOnSetProgressBounds;
        _worker.WorkFinished             += OnWorkerOnWorkFinished;
    }

    public ICommand CloseCommand { get; }

    void OnWorkerOnWorkFinished(object sender, MessageEventArgs args) => Dispatcher.UIThread.Post(() =>
    {
        StatusMessage   = args.Message;
        ProgressVisible = false;
        CanClose        = true;
    });

    void OnWorkerOnSetProgressBounds(object sender, ProgressBoundsEventArgs args) => Dispatcher.UIThread.Post(() =>
    {
        IndeterminateProgress = false;
        MaximumValue          = args.Maximum;
        MinimumValue          = args.Minimum;
    });

    void OnWorkerOnSetProgress(object sender, ProgressEventArgs args) =>
        Dispatcher.UIThread.Post(() => CurrentValue = args.Value);

    void OnWorkerOnSetMessage(object sender, MessageEventArgs args) =>
        Dispatcher.UIThread.Post(() => StatusMessage = args.Message);

    void OnWorkerOnSetIndeterminateProgress(object sender, EventArgs args) =>
        Dispatcher.UIThread.Post(() => IndeterminateProgress = true);

    void OnWorkerOnErrorOccurred(object sender, ErrorEventArgs args) => Dispatcher.UIThread.Post(() =>
    {
        ErrorMessage    = args.Message;
        ProgressVisible = false;
        ErrorVisible    = true;
        CanClose        = true;
    });

    void ExecuteCloseCommand() => _view.Close();

    internal void OnOpened()
    {
        ProgressVisible     =  true;
        _worker.RomSetAdded += RomSetAdded;
        _                   =  Task.Run(_worker.Import);
    }

    public event EventHandler<RomSetEventArgs> RomSetAdded;
}