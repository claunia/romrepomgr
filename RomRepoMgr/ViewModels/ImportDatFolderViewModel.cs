using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reactive;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using ReactiveUI;
using RomRepoMgr.Core.EventArgs;
using RomRepoMgr.Models;
using RomRepoMgr.Resources;

namespace RomRepoMgr.ViewModels;

public class ImportDatFolderViewModel : ViewModelBase
{
    readonly Stopwatch _stopwatch = new();
    bool               _allFilesChecked;
    bool               _canClose;
    bool               _canStart;
    string             _category;
    string[]           _datFiles;
    string             _folderPath;
    bool               _isImporting;
    bool               _isReady;
    int                _listPosition;
    bool               _progressIsIndeterminate;
    double             _progressMaximum;
    double             _progressMinimum;
    double             _progressValue;
    bool               _progressVisible;
    bool               _recursiveChecked;
    string             _statusMessage;
    int                _workers;

    public ImportDatFolderViewModel()
    {
        CanClose            = true;
        IsReady             = true;
        SelectFolderCommand = ReactiveCommand.CreateFromTask(SelectFolderAsync);
        CloseCommand        = ReactiveCommand.Create(Close);
        StartCommand        = ReactiveCommand.Create(Start);
    }

    public ReactiveCommand<Unit, Unit> SelectFolderCommand { get; }
    public Window                      View                { get; init; }

    public bool IsReady
    {
        get => _isReady;
        set => this.RaiseAndSetIfChanged(ref _isReady, value);
    }

    public string FolderPath
    {
        get => _folderPath;
        set => this.RaiseAndSetIfChanged(ref _folderPath, value);
    }

    public string Category
    {
        get => _category;
        set => this.RaiseAndSetIfChanged(ref _category, value);
    }

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

    public bool ProgressVisible
    {
        get => _progressVisible;
        set => this.RaiseAndSetIfChanged(ref _progressVisible, value);
    }

    public bool ProgressIsIndeterminate
    {
        get => _progressIsIndeterminate;
        set => this.RaiseAndSetIfChanged(ref _progressIsIndeterminate, value);
    }

    public string StatusMessage
    {
        get => _statusMessage;
        set => this.RaiseAndSetIfChanged(ref _statusMessage, value);
    }

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

    public bool IsImporting
    {
        get => _isImporting;
        set => this.RaiseAndSetIfChanged(ref _isImporting, value);
    }

    public ReactiveCommand<Unit, Unit>       CloseCommand { get; }
    public ReactiveCommand<Unit, Unit>       StartCommand { get; }
    public ObservableCollection<DatImporter> Importers    { get; } = [];

    void Start()
    {
        _listPosition           = 0;
        ProgressMinimum         = 0;
        ProgressMaximum         = _datFiles.Length;
        ProgressValue           = 0;
        ProgressIsIndeterminate = false;
        ProgressVisible         = true;
        CanClose                = false;
        CanStart                = false;
        IsReady                 = false;
        IsImporting             = true;
        _workers                = 0;
        _stopwatch.Restart();

        Import();
    }

    void Import()
    {
        Dispatcher.UIThread.Post(() =>
        {
            if(_listPosition >= _datFiles.Length)
            {
                if(_workers != 0) return;

                ProgressVisible = false;
                StatusMessage   = Localization.Finished;
                CanClose        = true;
                CanStart        = false;
                IsReady         = true;
                _stopwatch.Stop();

                return;
            }

            StatusMessage = string.Format(Localization.ImportingItem, Path.GetFileName(_datFiles[_listPosition]));
            ProgressValue = _listPosition;

            var model = new DatImporter
            {
                Filename      = Path.GetFileName(_datFiles[_listPosition]),
                Minimum       = 0,
                Maximum       = _datFiles.Length,
                Progress      = 0,
                Indeterminate = false
            };

            var worker = new Core.Workers.DatImporter(_datFiles[_listPosition], Category);
            worker.ErrorOccurred            += model.OnErrorOccurred;
            worker.SetIndeterminateProgress += model.OnSetIndeterminateProgress;
            worker.SetMessage               += model.OnSetMessage;
            worker.SetProgress              += model.OnSetProgress;
            worker.SetProgressBounds        += model.OnSetProgressBounds;
            worker.WorkFinished             += model.OnWorkFinished;
            worker.RomSetAdded              += RomSetAdded;

            worker.WorkFinished += (_, _) =>
            {
                _workers--;

                if(_workers < Environment.ProcessorCount) Import();
            };

            Importers.Add(model);

            model.Task = Task.Run(worker.Import);

            _workers++;
            _listPosition++;

            if(_workers < Environment.ProcessorCount) Import();
        });
    }

    public event EventHandler<RomSetEventArgs> RomSetAdded;

    void Close()
    {
        View.Close();
    }

    async Task SelectFolderAsync()
    {
        IReadOnlyList<IStorageFolder> result =
            await View.StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
            {
                Title = Localization.ImportDatFolderDialogTitle
            });

        if(result.Count < 1) return;

        FolderPath       = result[0].TryGetLocalPath() ?? string.Empty;
        RecursiveChecked = true;
        AllFilesChecked  = false;
        RefreshFiles();
    }

    void RefreshFiles()
    {
        _ = Task.Run(() =>
        {
            Dispatcher.UIThread.Post(() =>
            {
                IsReady                 = false;
                ProgressVisible         = true;
                ProgressIsIndeterminate = true;
                StatusMessage           = Localization.SearchingForFiles;
            });

            if(_allFilesChecked)
            {
                _datFiles = Directory.GetFiles(FolderPath,
                                               "*.*",
                                               _recursiveChecked
                                                   ? SearchOption.AllDirectories
                                                   : SearchOption.TopDirectoryOnly)
                                     .Order()
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

                _datFiles = dats.Concat(xmls).Order().ToArray();
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
}