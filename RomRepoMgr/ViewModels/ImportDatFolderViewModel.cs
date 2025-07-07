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
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Reactive;
using System.Threading.Tasks;
using Avalonia.Threading;
using ReactiveUI;
using RomRepoMgr.Core.EventArgs;
using RomRepoMgr.Core.Workers;
using RomRepoMgr.Resources;
using RomRepoMgr.Views;
using ErrorEventArgs = RomRepoMgr.Core.EventArgs.ErrorEventArgs;

namespace RomRepoMgr.ViewModels;

public sealed class ImportDatFolderViewModel : ViewModelBase
{
    readonly ImportDatFolder _view;
    bool                     _allFilesChecked;
    bool                     _canClose;
    bool                     _canStart;
    string                   _category;
    string[]                 _datFiles;
    bool                     _isImporting;
    bool                     _isReady;
    int                      _listPosition;
    bool                     _progress2IsIndeterminate;
    double                   _progress2Maximum;
    double                   _progress2Minimum;
    double                   _progress2Value;
    bool                     _progress2Visible;
    bool                     _progressIsIndeterminate;
    double                   _progressMaximum;
    double                   _progressMinimum;
    double                   _progressValue;
    bool                     _progressVisible;
    bool                     _recursiveChecked;
    string                   _status2Message;
    string                   _statusMessage;

    public ImportDatFolderViewModel(ImportDatFolder view, string folderPath)
    {
        _view             = view;
        FolderPath        = folderPath;
        _allFilesChecked  = false;
        _recursiveChecked = true;
        ImportResults     = [];
        CloseCommand      = ReactiveCommand.Create(ExecuteCloseCommand);
        StartCommand      = ReactiveCommand.Create(ExecuteStartCommand);
    }

    public string PathLabel      => Localization.PathLabel;
    public string CategoryLabel  => Localization.RomSetCategoryLabel;
    public string FolderPath     { get; }
    public string AllFilesLabel  => Localization.AllFilesLabel;
    public string RecursiveLabel => Localization.RecursiveLabel;

    public bool AllFilesChecked
    {
        get => _allFilesChecked;
        set
        {
            this.RaiseAndSetIfChanged(ref _allFilesChecked, value);
            RefreshFiles();
        }
    }

    public bool RecursiveChecked
    {
        get => _recursiveChecked;
        set
        {
            this.RaiseAndSetIfChanged(ref _recursiveChecked, value);
            RefreshFiles();
        }
    }

    public bool IsReady
    {
        get => _isReady;
        set => this.RaiseAndSetIfChanged(ref _isReady, value);
    }

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

    public bool IsImporting
    {
        get => _isImporting;
        set => this.RaiseAndSetIfChanged(ref _isImporting, value);
    }

    public string Category
    {
        get => _category;
        set => this.RaiseAndSetIfChanged(ref _category, value);
    }

    public string Title => Localization.ImportDatFolderTitle;

    public ObservableCollection<ImportDatFolderItem> ImportResults       { get; }
    public string                                    ResultFilenameLabel => Localization.ResultFilenameLabel;
    public string                                    ResultStatusLabel   => Localization.ResultStatusLabel;
    public string                                    CloseLabel          => Localization.CloseLabel;
    public string                                    StartLabel          => Localization.StartLabel;

    public bool CanClose
    {
        get => _canClose;
        set => this.RaiseAndSetIfChanged(ref _canClose, value);
    }

    public bool CanStart
    {
        get => _canStart;
        set => this.RaiseAndSetIfChanged(ref _canStart, value);
    }

    public ReactiveCommand<Unit, Unit> CloseCommand { get; }
    public ReactiveCommand<Unit, Unit> StartCommand { get; }

    internal void OnOpened() => RefreshFiles();

    void RefreshFiles()
    {
        _ = Task.Run(() =>
        {
            Dispatcher.UIThread.Post(() =>
            {
                IsReady                 = false;
                ProgressVisible         = true;
                Progress2Visible        = false;
                ProgressIsIndeterminate = true;
                StatusMessage           = Localization.SearchingForFiles;
            });

            if(_allFilesChecked)
            {
                _datFiles = Directory
                           .GetFiles(FolderPath,
                                     "*.*",
                                     _recursiveChecked ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly)
                           .OrderBy(f => f)
                           .ToArray();
            }
            else
            {
                string[] dats = Directory.GetFiles(FolderPath,
                                                   "*.dat",
                                                   _recursiveChecked
                                                       ? SearchOption.AllDirectories
                                                       : SearchOption.TopDirectoryOnly);

                string[] xmls = Directory.GetFiles(FolderPath,
                                                   "*.xml",
                                                   _recursiveChecked
                                                       ? SearchOption.AllDirectories
                                                       : SearchOption.TopDirectoryOnly);

                _datFiles = dats.Concat(xmls).OrderBy(f => f).ToArray();
            }

            Dispatcher.UIThread.Post(() =>
            {
                IsReady         = true;
                ProgressVisible = false;
                StatusMessage   = string.Format(Localization.FoundFiles, _datFiles.Length);
                CanClose        = true;
                CanStart        = true;
            });
        });
    }

    void ExecuteCloseCommand() => _view.Close();

    void ExecuteStartCommand()
    {
        _listPosition           = 0;
        ProgressMinimum         = 0;
        ProgressMaximum         = _datFiles.Length;
        ProgressValue           = 0;
        ProgressIsIndeterminate = false;
        ProgressVisible         = true;
        Progress2Visible        = true;
        CanClose                = false;
        CanStart                = false;
        IsReady                 = false;
        IsImporting             = true;

        Import();
    }

    void Import()
    {
        if(_listPosition >= _datFiles.Length)
        {
            Progress2Visible = false;
            ProgressVisible  = false;
            StatusMessage    = Localization.Finished;
            CanClose         = true;
            CanStart         = false;
            IsReady          = true;

            return;
        }

        StatusMessage = string.Format(Localization.ImportingItem, Path.GetFileName(_datFiles[_listPosition]));
        ProgressValue = _listPosition;

        var _worker = new DatImporter(_datFiles[_listPosition], Category);
        _worker.ErrorOccurred            += OnWorkerOnErrorOccurred;
        _worker.SetIndeterminateProgress += OnWorkerOnSetIndeterminateProgress;
        _worker.SetMessage               += OnWorkerOnSetMessage;
        _worker.SetProgress              += OnWorkerOnSetProgress;
        _worker.SetProgressBounds        += OnWorkerOnSetProgressBounds;
        _worker.WorkFinished             += OnWorkerOnWorkFinished;
        _worker.RomSetAdded              += RomSetAdded;
        _                                =  Task.Run(_worker.Import);
    }

    void OnWorkerOnWorkFinished(object sender, MessageEventArgs args) => Dispatcher.UIThread.Post(() =>
    {
        ImportResults.Add(new ImportDatFolderItem
        {
            Filename = Path.GetFileName(_datFiles[_listPosition]),
            Status   = args.Message
        });

        _listPosition++;
        Import();
    });

    void OnWorkerOnSetProgressBounds(object sender, ProgressBoundsEventArgs args) => Dispatcher.UIThread.Post(() =>
    {
        Progress2IsIndeterminate = false;
        Progress2Maximum         = args.Maximum;
        Progress2Minimum         = args.Minimum;
    });

    void OnWorkerOnSetProgress(object sender, ProgressEventArgs args) =>
        Dispatcher.UIThread.Post(() => Progress2Value = args.Value);

    void OnWorkerOnSetMessage(object sender, MessageEventArgs args) =>
        Dispatcher.UIThread.Post(() => Status2Message = args.Message);

    void OnWorkerOnSetIndeterminateProgress(object sender, EventArgs args) =>
        Dispatcher.UIThread.Post(() => Progress2IsIndeterminate = true);

    void OnWorkerOnErrorOccurred(object sender, ErrorEventArgs args) => Dispatcher.UIThread.Post(() =>
    {
        ImportResults.Add(new ImportDatFolderItem
        {
            Filename = Path.GetFileName(_datFiles[_listPosition]),
            Status   = args.Message
        });

        _listPosition++;
        Import();
    });

    public event EventHandler<RomSetEventArgs> RomSetAdded;
}

public sealed class ImportDatFolderItem
{
    public string Filename { get; set; }
    public string Status   { get; set; }
}