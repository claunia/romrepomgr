using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using Avalonia.Controls;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using RomRepoMgr.Core.EventArgs;
using RomRepoMgr.Models;
using RomRepoMgr.Resources;
using Serilog;
using Serilog.Extensions.Logging;

namespace RomRepoMgr.ViewModels;

public sealed partial class ImportDatFolderViewModel : ViewModelBase
{
    readonly Stopwatch _stopwatch = new();
    bool               _allFilesChecked;
    [ObservableProperty]
    bool _canClose;
    [ObservableProperty]
    bool _canStart;
    [ObservableProperty]
    string _category;
    string[] _datFiles;
    [ObservableProperty]
    string _folderPath;
    [ObservableProperty]
    bool _isImporting;
    [ObservableProperty]
    bool _isReady;
    int _listPosition;
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
    bool _recursiveChecked;
    [ObservableProperty]
    string _statusMessage;
    int _workers;

    public ImportDatFolderViewModel()
    {
        CanClose            = true;
        IsReady             = true;
        SelectFolderCommand = new AsyncRelayCommand(SelectFolderAsync);
        CloseCommand        = new RelayCommand(Close);
        StartCommand        = new RelayCommand(Start);
    }

    public ICommand SelectFolderCommand { get; }
    public Window   View                { get; init; }

    public bool AllFilesChecked
    {
        get => _allFilesChecked;
        set
        {
            SetProperty(ref _allFilesChecked, value);
            RefreshFiles();
        }
    }

    public bool RecursiveChecked
    {
        get => _recursiveChecked;
        set
        {
            SetProperty(ref _recursiveChecked, value);
            RefreshFiles();
        }
    }

    public ICommand                          CloseCommand { get; }
    public ICommand                          StartCommand { get; }
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

            var worker =
                new Core.Workers.DatImporter(_datFiles[_listPosition], Category, new SerilogLoggerFactory(Log.Logger));

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