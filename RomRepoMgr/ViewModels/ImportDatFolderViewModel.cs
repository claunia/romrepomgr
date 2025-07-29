using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
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
        _stopwatch.Restart();

        _ = Task.Run(Import);
    }

    void Import()
    {
        Parallel.ForEach(_datFiles,
                         new ParallelOptions
                         {
                             MaxDegreeOfParallelism = Environment.ProcessorCount
                         },
                         datFile =>
                         {
                             Dispatcher.UIThread.Post(() =>
                             {
                                 StatusMessage = string.Format(Localization.ImportingItem, Path.GetFileName(datFile));

                                 ProgressValue = _listPosition;
                             });

                             var model = new DatImporter
                             {
                                 Filename      = Path.GetFileName(datFile),
                                 Minimum       = 0,
                                 Maximum       = _datFiles.Length,
                                 Progress      = 0,
                                 Indeterminate = false
                             };

                             var worker = new Core.Workers.DatImporter(datFile,
                                                                       Category,
                                                                       new SerilogLoggerFactory(Log.Logger));

                             worker.ErrorOccurred            += model.OnErrorOccurred;
                             worker.SetIndeterminateProgress += model.OnSetIndeterminateProgress;
                             worker.SetMessage               += model.OnSetMessage;
                             worker.SetProgress              += model.OnSetProgress;
                             worker.SetProgressBounds        += model.OnSetProgressBounds;
                             worker.WorkFinished             += model.OnWorkFinished;
                             worker.RomSetAdded              += RomSetAdded;

                             Dispatcher.UIThread.Post(() => Importers.Add(model));

                             worker.Import();

                             Interlocked.Increment(ref _listPosition);
                         });

        Dispatcher.UIThread.Post(() =>
        {
            ProgressVisible = false;
            StatusMessage   = Localization.Finished;
            CanClose        = true;
            CanStart        = false;
            IsReady         = true;
            _stopwatch.Stop();
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