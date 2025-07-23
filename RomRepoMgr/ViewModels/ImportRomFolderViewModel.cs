using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Reactive;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using ReactiveUI;
using RomRepoMgr.Core.EventArgs;
using RomRepoMgr.Core.Workers;
using RomRepoMgr.Models;
using RomRepoMgr.Resources;

namespace RomRepoMgr.ViewModels;

public class ImportRomFolderViewModel : ViewModelBase
{
    bool               _canClose;
    bool               _canStart;
    string             _folderPath;
    bool               _isImporting;
    bool               _isReady;
    bool               _knownOnlyChecked;
    int                _listPosition;
    bool               _progress2IsIndeterminate;
    double             _progress2Maximum;
    double             _progress2Minimum;
    double             _progress2Value;
    bool               _progress2Visible;
    bool               _progressIsIndeterminate;
    double             _progressMaximum;
    double             _progressMinimum;
    double             _progressValue;
    bool               _progressVisible;
    bool               _recurseArchivesChecked;
    bool               _removeFilesChecked;
    bool               _removeFilesEnabled;
    FileImporter       _rootImporter;
    string             _statusMessage;
    string             _statusMessage2;
    bool               _statusMessage2Visible;
    readonly Stopwatch _stopwatch = new();

    public ImportRomFolderViewModel()
    {
        SelectFolderCommand    = ReactiveCommand.CreateFromTask(SelectFolderAsync);
        CloseCommand           = ReactiveCommand.Create(Close);
        StartCommand           = ReactiveCommand.Create(Start);
        CanClose               = true;
        RemoveFilesChecked     = false;
        KnownOnlyChecked       = true;
        RecurseArchivesChecked = Settings.Settings.UnArUsable;
        RemoveFilesEnabled     = false;
    }

    public ReactiveCommand<Unit, Unit> SelectFolderCommand { get; }
    public ReactiveCommand<Unit, Unit> CloseCommand        { get; }
    public ReactiveCommand<Unit, Unit> StartCommand        { get; }
    public Window                      View                { get; init; }

    public bool RecurseArchivesEnabled => Settings.Settings.UnArUsable;

    public bool RemoveFilesChecked
    {
        get => _removeFilesChecked;
        set => this.RaiseAndSetIfChanged(ref _removeFilesChecked, value);
    }

    public bool KnownOnlyChecked
    {
        get => _knownOnlyChecked;
        set => this.RaiseAndSetIfChanged(ref _knownOnlyChecked, value);
    }

    public bool RemoveFilesEnabled
    {
        get => _removeFilesEnabled;
        set => this.RaiseAndSetIfChanged(ref _removeFilesEnabled, value);
    }

    public bool RecurseArchivesChecked
    {
        get => _recurseArchivesChecked;
        set
        {
            if(value) RemoveFilesChecked = false;

            RemoveFilesEnabled = !value;
            this.RaiseAndSetIfChanged(ref _recurseArchivesChecked, value);
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

    public bool StatusMessage2Visible
    {
        get => _statusMessage2Visible;
        set => this.RaiseAndSetIfChanged(ref _statusMessage2Visible, value);
    }

    public string StatusMessage2
    {
        get => _statusMessage2;
        set => this.RaiseAndSetIfChanged(ref _statusMessage2, value);
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

    public string FolderPath
    {
        get => _folderPath;
        set => this.RaiseAndSetIfChanged(ref _folderPath, value);
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

    public bool IsImporting
    {
        get => _isImporting;
        set => this.RaiseAndSetIfChanged(ref _isImporting, value);
    }


    public ObservableCollection<RomImporter> Importers { get; } = [];

    void Start()
    {
        _rootImporter                          =  new FileImporter(KnownOnlyChecked, RemoveFilesChecked);
        _rootImporter.SetMessage               += SetMessage;
        _rootImporter.SetIndeterminateProgress += SetIndeterminateProgress;
        _rootImporter.SetProgress              += SetProgress;
        _rootImporter.SetProgressBounds        += SetProgressBounds;
        _rootImporter.Finished                 += EnumeratingFilesFinished;
        ProgressIsIndeterminate                =  true;
        ProgressVisible                        =  true;
        CanClose                               =  false;
        CanStart                               =  false;
        IsImporting                            =  true;

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

                             var worker = new FileImporter(KnownOnlyChecked, RemoveFilesChecked);
                             worker.SetIndeterminateProgress2 += model.OnSetIndeterminateProgress;
                             worker.SetMessage2               += model.OnSetMessage;
                             worker.SetProgress2              += model.OnSetProgress;
                             worker.SetProgressBounds2        += model.OnSetProgressBounds;
                             worker.ImportedRom               += model.OnImportedRom;
                             worker.WorkFinished              += model.OnWorkFinished;

                             Dispatcher.UIThread.Post(() => Importers.Add(model));

                             worker.ImportFile(file);

                             worker.SaveChanges();
                             Interlocked.Increment(ref _listPosition);
                         });

        _stopwatch.Stop();
        Console.WriteLine("Took " + _stopwatch.Elapsed.TotalSeconds + " seconds to process files.");

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
        Progress2Visible        = true;
        StatusMessage2Visible   = true;
        _listPosition           = 0;
        _stopwatch.Restart();

        foreach(string archive in _rootImporter.Archives)
        {
            StatusMessage = "Processing archive: " + Path.GetFileName(archive);
            ProgressValue = _listPosition++;

            // Create FileImporter
            var archiveImporter = new FileImporter(KnownOnlyChecked, RemoveFilesChecked);

            archiveImporter.SetIndeterminateProgress2 += SetIndeterminateProgress2;
            archiveImporter.SetMessage2               += SetMessage2;
            archiveImporter.SetProgress2              += SetProgress2;
            archiveImporter.SetProgressBounds2        += SetProgress2Bounds;

            // Extract archive
            bool ret = archiveImporter.ExtractArchive(archive);

            if(!ret) continue;

            // Process files in archive
            Parallel.ForEach(archiveImporter.Files,
                             file =>
                             {
                                 var model = new RomImporter
                                 {
                                     Filename      = Path.GetFileName(file),
                                     Indeterminate = true
                                 };

                                 var worker = new FileImporter(KnownOnlyChecked, RemoveFilesChecked);
                                 worker.SetIndeterminateProgress2 += model.OnSetIndeterminateProgress;
                                 worker.SetMessage2               += model.OnSetMessage;
                                 worker.SetProgress2              += model.OnSetProgress;
                                 worker.SetProgressBounds2        += model.OnSetProgressBounds;
                                 worker.ImportedRom               += model.OnImportedRom;
                                 worker.WorkFinished              += model.OnWorkFinished;

                                 Dispatcher.UIThread.Post(() => Importers.Add(model));

                                 worker.ImportFile(file);

                                 worker.SaveChanges();

                                 worker.Files.Clear();
                             });

            // Remove temporary files
            archiveImporter.CleanupExtractedArchive();

            // Save database changes
            archiveImporter.SaveChanges();
        }

        _stopwatch.Stop();
        Console.WriteLine("Took " + _stopwatch.Elapsed.TotalSeconds + " seconds to process archives.");

        Progress2Visible      = false;
        StatusMessage2Visible = false;

        ProcessFiles();
    }

    void CheckArchivesFinished(object sender, EventArgs e)
    {
        _stopwatch.Stop();
        Console.WriteLine("Took {0} seconds to check archives.", _stopwatch.Elapsed.TotalSeconds);

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