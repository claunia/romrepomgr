using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using Avalonia.Controls;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using RomRepoMgr.Core.EventArgs;
using RomRepoMgr.Core.Workers;
using RomRepoMgr.Database;
using RomRepoMgr.Database.Models;
using RomRepoMgr.Models;
using RomRepoMgr.Resources;
using Serilog;
using Serilog.Extensions.Logging;

namespace RomRepoMgr.ViewModels;

public sealed partial class ImportRomFolderViewModel : ViewModelBase
{
    readonly Context _ctx =
        Context.Create(Settings.Settings.Current.DatabasePath, new SerilogLoggerFactory(Log.Logger));
    readonly ConcurrentBag<DbDisk>  _newDisks  = [];
    readonly ConcurrentBag<DbFile>  _newFiles  = [];
    readonly ConcurrentBag<DbMedia> _newMedias = [];
    readonly Stopwatch              _stopwatch = new();
    [ObservableProperty]
    bool _canChoose;
    [ObservableProperty]
    bool _canClose;
    [ObservableProperty]
    bool _canStart;
    [ObservableProperty]
    string _folderPath;
    [ObservableProperty]
    bool _isImporting;
    [ObservableProperty]
    bool _isReady;
    [ObservableProperty]
    bool _knownOnlyChecked;
    int _listPosition;
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
    bool _progressIsIndeterminate;
    [ObservableProperty]
    double _progressMaximum;
    [ObservableProperty]
    double _progressMinimum;
    [ObservableProperty]
    double _progressValue;
    [ObservableProperty]
    bool _progressVisible;
    bool _recurseArchivesChecked;
    [ObservableProperty]
    bool _removeFilesChecked;
    [ObservableProperty]
    bool _removeFilesEnabled;
    FileImporter _rootImporter;
    [ObservableProperty]
    string _statusMessage;
    [ObservableProperty]
    string _statusMessage2;
    [ObservableProperty]
    bool _statusMessage2Visible;

    public ImportRomFolderViewModel()
    {
        SelectFolderCommand    = new AsyncRelayCommand(SelectFolderAsync);
        CloseCommand           = new RelayCommand(Close);
        StartCommand           = new RelayCommand(Start);
        CanClose               = true;
        RemoveFilesChecked     = false;
        KnownOnlyChecked       = true;
        RecurseArchivesChecked = Settings.Settings.UnArUsable;
        RemoveFilesEnabled     = false;
        CanChoose              = true;
    }

    public ICommand SelectFolderCommand { get; }
    public ICommand CloseCommand        { get; }
    public ICommand StartCommand        { get; }
    public Window   View                { get; init; }

    public bool RecurseArchivesEnabled => Settings.Settings.UnArUsable;

    public bool RecurseArchivesChecked
    {
        get => _recurseArchivesChecked;
        set
        {
            if(value) RemoveFilesChecked = false;

            RemoveFilesEnabled = !value;
            SetProperty(ref _recurseArchivesChecked, value);
        }
    }

    public ObservableCollection<RomImporter> Importers { get; } = [];

    void Start()
    {
        _rootImporter = new FileImporter(_ctx, _newFiles, _newDisks, _newMedias, KnownOnlyChecked, RemoveFilesChecked);
        _rootImporter.SetMessage += SetMessage;
        _rootImporter.SetIndeterminateProgress += SetIndeterminateProgress;
        _rootImporter.SetProgress += SetProgress;
        _rootImporter.SetProgressBounds += SetProgressBounds;
        _rootImporter.Finished += EnumeratingFilesFinished;
        ProgressIsIndeterminate = true;
        ProgressVisible = true;
        CanClose = false;
        CanStart = false;
        IsImporting = true;
        IsReady = false;
        CanChoose = false;

        _ = Task.Run(() => _rootImporter.FindFiles(FolderPath));
    }

    void SetProgressBounds(object sender, ProgressBoundsEventArgs e) => Dispatcher.UIThread.Post(() =>
    {
        ProgressIsIndeterminate = false;
        ProgressMaximum         = e.Maximum;
        ProgressMinimum         = e.Minimum;
    });

    void SetProgress(object sender, ProgressEventArgs e)
    {
        Dispatcher.UIThread.Post(() => ProgressValue = e.Value);
    }

    void SetIndeterminateProgress(object sender, EventArgs e)
    {
        Dispatcher.UIThread.Post(() => ProgressIsIndeterminate = true);
    }

    void SetProgress2Bounds(object sender, ProgressBoundsEventArgs e) => Dispatcher.UIThread.Post(() =>
    {
        Progress2IsIndeterminate = false;
        Progress2Maximum         = e.Maximum;
        Progress2Minimum         = e.Minimum;
    });

    void SetProgress2(object sender, ProgressEventArgs e)
    {
        Dispatcher.UIThread.Post(() => Progress2Value = e.Value);
    }

    void SetIndeterminateProgress2(object sender, EventArgs e)
    {
        Dispatcher.UIThread.Post(() => Progress2IsIndeterminate = true);
    }

    void EnumeratingFilesFinished(object sender, EventArgs e)
    {
        _rootImporter.Finished -= EnumeratingFilesFinished;

        if(RecurseArchivesChecked)
        {
            Progress2Visible                        =  true;
            StatusMessage2Visible                   =  true;
            _rootImporter.SetMessage2               += SetMessage2;
            _rootImporter.SetIndeterminateProgress2 += SetIndeterminateProgress2;
            _rootImporter.SetProgress2              += SetProgress2;
            _rootImporter.SetProgressBounds2        += SetProgress2Bounds;

            _rootImporter.Finished += CheckArchivesFinished;

            _ = Task.Run(() =>
            {
                _stopwatch.Restart();
                _rootImporter.SeparateFilesAndArchives();
            });
        }
        else
            ProcessFiles();
    }

    void ProcessFiles()
    {
        _listPosition           = 0;
        ProgressMinimum         = 0;
        ProgressMaximum         = _rootImporter.Files.Count;
        ProgressValue           = 0;
        ProgressIsIndeterminate = false;
        ProgressVisible         = true;
        CanClose                = false;
        CanStart                = false;
        IsReady                 = false;
        IsImporting             = true;
        _stopwatch.Restart();

        Parallel.ForEach(_rootImporter.Files,
                         file =>
                         {
                             Dispatcher.UIThread.Post(() =>
                             {
                                 StatusMessage = string.Format(Localization.ImportingItem, Path.GetFileName(file));
                                 ProgressValue = _listPosition;
                             });

                             var model = new RomImporter
                             {
                                 Filename      = Path.GetFileName(file),
                                 Indeterminate = true
                             };

                             var worker = new FileImporter(_ctx,
                                                           _newFiles,
                                                           _newDisks,
                                                           _newMedias,
                                                           KnownOnlyChecked,
                                                           RemoveFilesChecked);

                             worker.SetIndeterminateProgress2 += model.OnSetIndeterminateProgress;
                             worker.SetMessage2               += model.OnSetMessage;
                             worker.SetProgress2              += model.OnSetProgress;
                             worker.SetProgressBounds2        += model.OnSetProgressBounds;
                             worker.ImportedRom               += model.OnImportedRom;
                             worker.WorkFinished              += model.OnWorkFinished;

                             Dispatcher.UIThread.Post(() => Importers.Add(model));

                             worker.ImportFile(file);

                             Interlocked.Increment(ref _listPosition);
                         });

        _stopwatch.Stop();
        Log.Debug("Took {TotalSeconds} seconds to process files", _stopwatch.Elapsed.TotalSeconds);

        _rootImporter.SaveChanges();

        _rootImporter.UpdateRomStats();

        _listPosition           = 0;
        ProgressMinimum         = 0;
        ProgressMaximum         = 1;
        ProgressValue           = 0;
        ProgressIsIndeterminate = false;
        ProgressVisible         = false;
        CanClose                = true;
        CanStart                = false;
        IsReady                 = false;
        IsImporting             = false;
        StatusMessage           = Localization.Finished;
    }

    void ProcessArchives()
    {
        // For each archive
        ProgressMaximum         = _rootImporter.Archives.Count;
        ProgressMinimum         = 0;
        ProgressValue           = 0;
        ProgressIsIndeterminate = false;
        Progress2Visible        = false;
        StatusMessage2Visible   = false;
        _listPosition           = 0;
        _stopwatch.Restart();

        Parallel.ForEach(_rootImporter.Archives,
                         archive =>
                         {
                             Dispatcher.UIThread.Post(() =>
                             {
                                 StatusMessage = "Processing archive: " + Path.GetFileName(archive);
                                 ProgressValue = _listPosition;
                             });

                             // Create FileImporter
                             var archiveImporter = new FileImporter(_ctx,
                                                                    _newFiles,
                                                                    _newDisks,
                                                                    _newMedias,
                                                                    KnownOnlyChecked,
                                                                    RemoveFilesChecked);

                             // Extract archive
                             bool ret = archiveImporter.ExtractArchive(archive);

                             if(!ret) return;

                             // Process files in archive
                             foreach(string file in archiveImporter.Files)
                             {
                                 var model = new RomImporter
                                 {
                                     Filename      = Path.GetFileName(file),
                                     Indeterminate = true
                                 };

                                 var worker = new FileImporter(_ctx,
                                                               _newFiles,
                                                               _newDisks,
                                                               _newMedias,
                                                               KnownOnlyChecked,
                                                               RemoveFilesChecked);

                                 worker.SetIndeterminateProgress2 += model.OnSetIndeterminateProgress;
                                 worker.SetMessage2               += model.OnSetMessage;
                                 worker.SetProgress2              += model.OnSetProgress;
                                 worker.SetProgressBounds2        += model.OnSetProgressBounds;
                                 worker.ImportedRom               += model.OnImportedRom;
                                 worker.WorkFinished              += model.OnWorkFinished;

                                 Dispatcher.UIThread.Post(() => Importers.Add(model));

                                 worker.ImportFile(file);

                                 worker.Files.Clear();
                             }

                             // Remove temporary files
                             archiveImporter.CleanupExtractedArchive();

                             Interlocked.Increment(ref _listPosition);
                         });

        _stopwatch.Stop();
        Log.Debug("Took {TotalSeconds} seconds to process archives", _stopwatch.Elapsed.TotalSeconds);

        Progress2Visible      = false;
        StatusMessage2Visible = false;

        ProcessFiles();
    }

    void CheckArchivesFinished(object sender, EventArgs e)
    {
        _stopwatch.Stop();
        Log.Debug("Took {TotalSeconds} seconds to check archives", _stopwatch.Elapsed.TotalSeconds);

        Progress2Visible      = false;
        StatusMessage2Visible = false;

        _rootImporter.Finished -= CheckArchivesFinished;

        ProcessArchives();
    }

    void SetMessage(object sender, MessageEventArgs e)
    {
        Dispatcher.UIThread.Post(() => StatusMessage = e.Message);
    }

    void SetMessage2(object sender, MessageEventArgs e)
    {
        Dispatcher.UIThread.Post(() => StatusMessage2 = e.Message);
    }

    void Close() => View.Close();

    async Task SelectFolderAsync()
    {
        IReadOnlyList<IStorageFolder> result =
            await View.StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
            {
                Title = Localization.ImportRomsFolderDialogTitle
            });

        if(result.Count < 1) return;

        FolderPath = result[0].TryGetLocalPath() ?? string.Empty;

        IsReady  = true;
        CanStart = true;
        CanClose = true;
    }
}