using System.Collections.Concurrent;
using System.Diagnostics;
using Microsoft.AspNetCore.Components;
using RomRepoMgr.Core.EventArgs;
using RomRepoMgr.Core.Workers;
using RomRepoMgr.Database.Models;
using Serilog;
using SharpCompress.Readers;

namespace RomRepoMgr.Blazor.Components.Dialogs;

public partial class ImportRoms : ComponentBase
{
    readonly Stopwatch              _mainStopwatch = new();
    readonly ConcurrentBag<DbDisk>  _newDisks      = [];
    readonly ConcurrentBag<DbFile>  _newFiles      = [];
    readonly ConcurrentBag<DbMedia> _newMedias     = [];
    readonly Stopwatch              _stopwatch     = new();
    int                             _listPosition;
    FileImporter                    _rootImporter;
    PaginationState?                pagination;
    [CascadingParameter]
    public FluentDialog Dialog { get;            set; }
    public bool    IsBusy                 { get; set; }
    public bool    NotYetStarted          { get; set; }
    public bool    RemoveFilesChecked     { get; set; }
    public bool    KnownOnlyChecked       { get; set; }
    public bool    RecurseArchivesChecked { get; set; }
    public Color?  StatusMessageColor     { get; set; }
    public string? StatusMessage          { get; set; }
    public int?    ProgressMax            { get; set; }
    public int?    ProgressMin            { get; set; }
    public int?    ProgressValue          { get; set; }
    public string? StatusMessage2         { get; set; }
    public int?    Progress2Max           { get; set; }
    public int?    Progress2Min           { get; set; }
    public int?    Progress2Value         { get; set; }
    public string? FolderPath             { get; set; }
    public bool    Importing              { get; set; }
    public bool    Progress2Visible       { get; set; }
    public bool    DataGridVisible        { get; set; }
    public bool    CannotClose            { get; set; }

    protected override void OnInitialized()
    {
        base.OnInitialized();
        FolderPath             = Configuration["DataFolders:ImportRoms"] ?? "incoming";
        IsBusy                 = false;
        NotYetStarted          = true;
        RemoveFilesChecked     = false;
        KnownOnlyChecked       = true;
        RecurseArchivesChecked = true;
        StatusMessage          = "";
        Importing              = false;
        Progress2Visible       = false;
        DataGridVisible        = false;

        Logger.LogDebug("ImportRoms dialog initialized with Path: {Path}", FolderPath);
    }

    void Start()
    {
        _rootImporter = new FileImporter(_ctx, _newFiles, _newDisks, _newMedias, KnownOnlyChecked, RemoveFilesChecked);
        _rootImporter.SetMessage += SetMessage;
        _rootImporter.SetIndeterminateProgress += SetIndeterminateProgress;
        _rootImporter.SetProgress += SetProgress;
        _rootImporter.SetProgressBounds += SetProgressBounds;
        _rootImporter.Finished += EnumeratingFilesFinished;
        ProgressValue = null;
        Importing = true;
        CannotClose = true;
        IsBusy = true;
        NotYetStarted = false;
        _mainStopwatch.Start();

        _ = Task.Run(() => _rootImporter.FindFiles(FolderPath));
    }

    void SetProgressBounds(object? sender, ProgressBoundsEventArgs e)
    {
        _ = InvokeAsync(() =>
        {
            ProgressValue = 0;
            ProgressMax   = (int?)e.Maximum;
            ProgressMin   = (int?)e.Minimum;
        });
    }

    void SetProgress(object? sender, ProgressEventArgs e)
    {
        _ = InvokeAsync(() =>
        {
            ProgressValue = (int?)e.Value;
            StateHasChanged();
        });
    }

    void SetIndeterminateProgress(object? sender, EventArgs e)
    {
        _ = InvokeAsync(() =>
        {
            ProgressValue = null;
            StateHasChanged();
        });
    }

    void SetMessage(object? sender, MessageEventArgs e)
    {
        _ = InvokeAsync(() =>
        {
            StatusMessage = e.Message;
            StateHasChanged();
        });
    }

    Task CloseAsync() => Dialog.CloseAsync();

    void EnumeratingFilesFinished(object? sender, EventArgs e)
    {
        _rootImporter.Finished -= EnumeratingFilesFinished;

        if(RecurseArchivesChecked)
        {
            _ = InvokeAsync(() =>
            {
                Progress2Visible = true;
                StateHasChanged();
            });

            _rootImporter.SetMessage2               += SetMessage2;
            _rootImporter.SetIndeterminateProgress2 += SetIndeterminateProgress2;
            _rootImporter.SetProgress2              += SetProgress2;
            _rootImporter.SetProgressBounds2        += SetProgress2Bounds;

            _rootImporter.Finished += CheckArchivesFinished;

            _ = Task.Run(() =>
            {
                _stopwatch.Restart();

                _rootImporter.SeparateFilesAndArchivesManaged();
            });
        }
        else
            ProcessFiles();
    }

    void SetProgress2Bounds(object? sender, ProgressBoundsEventArgs e)
    {
        _ = InvokeAsync(() =>
        {
            Progress2Value = 0;
            Progress2Max   = (int?)e.Maximum;
            Progress2Min   = (int?)e.Minimum;
        });
    }

    void SetProgress2(object? sender, ProgressEventArgs e)
    {
        _ = InvokeAsync(() =>
        {
            Progress2Value = (int?)e.Value;
            StateHasChanged();
        });
    }

    void SetIndeterminateProgress2(object? sender, EventArgs e)
    {
        _ = InvokeAsync(() =>
        {
            Progress2Value = null;
            StateHasChanged();
        });
    }

    void SetMessage2(object? sender, MessageEventArgs e)
    {
        _ = InvokeAsync(() =>
        {
            StatusMessage2 = e.Message;
            StateHasChanged();
        });
    }

    void ProcessFiles()
    {
        _ = InvokeAsync(() =>
        {
            ProgressMin   = 0;
            ProgressMax   = _rootImporter.Files.Count;
            ProgressValue = 0;
            NotYetStarted = false;
            CannotClose   = true;
            IsBusy        = true;
            Importing     = true;

            StateHasChanged();
        });

        _listPosition = 0;
        _stopwatch.Restart();

        Parallel.ForEach(_rootImporter.Files,
                         file =>
                         {
                             _ = InvokeAsync(() =>
                             {
                                 StatusMessage = string.Format(Localizer["ImportingItem"], Path.GetFileName(file));
                                 ProgressValue = _listPosition;

                                 StateHasChanged();
                             });

                             var worker = new FileImporter(_ctx,
                                                           _newFiles,
                                                           _newDisks,
                                                           _newMedias,
                                                           KnownOnlyChecked,
                                                           RemoveFilesChecked);

                             worker.ImportFile(file);

                             Interlocked.Increment(ref _listPosition);
                         });

        _stopwatch.Stop();
        Log.Debug("Took {TotalSeconds} seconds to process files", _stopwatch.Elapsed.TotalSeconds);

        _rootImporter.SaveChanges();

        _rootImporter.UpdateRomStats();

        _ = InvokeAsync(() =>
        {
            ProgressMin   = 0;
            ProgressMax   = 1;
            ProgressValue = 0;
            CannotClose   = false;
            IsBusy        = false;
            Importing     = false;
            StatusMessage = Localizer["Finished"];

            StateHasChanged();
        });

        _listPosition = 0;
        _mainStopwatch.Stop();

        Log.Debug("Took {TotalSeconds} seconds to import ROMs", _mainStopwatch.Elapsed.TotalSeconds);
    }

    void CheckArchivesFinished(object? sender, EventArgs e)
    {
        _stopwatch.Stop();
        Log.Debug("Took {TotalSeconds} seconds to check archives", _stopwatch.Elapsed.TotalSeconds);

        _ = InvokeAsync(() =>
        {
            Progress2Visible = false;

            StateHasChanged();
        });

        _rootImporter.Finished -= CheckArchivesFinished;

        ProcessArchivesManaged();
    }

    void ProcessArchivesManaged()
    {
        _ = InvokeAsync(() =>
        {
            ProgressMax      = _rootImporter.Archives.Count;
            ProgressMin      = 0;
            ProgressValue    = 0;
            Progress2Visible = false;

            StateHasChanged();
        });

        _listPosition = 0;
        _stopwatch.Restart();

        // For each archive
        Parallel.ForEach(_rootImporter.Archives,
                         archive =>
                         {
                             _ = InvokeAsync(() =>
                             {
                                 StatusMessage =
                                     string.Format(Localizer["ProcessingArchive"], Path.GetFileName(archive));

                                 ProgressValue = _listPosition;

                                 StateHasChanged();
                             });

                             // Create FileImporter
                             var archiveImporter = new FileImporter(_ctx,
                                                                    _newFiles,
                                                                    _newDisks,
                                                                    _newMedias,
                                                                    KnownOnlyChecked,
                                                                    RemoveFilesChecked);

                             // Open archive
                             try
                             {
                                 using var     fs     = new FileStream(archive, FileMode.Open, FileAccess.Read);
                                 using IReader reader = ReaderFactory.Open(fs);

                                 // Process files in archive
                                 while(reader.MoveToNextEntry())
                                 {
                                     if(reader.Entry.IsDirectory) continue;

                                     if(reader.Entry.Crc == 0 && KnownOnlyChecked) continue;

                                     if(!archiveImporter.IsCrcInDb(reader.Entry.Crc) && KnownOnlyChecked) continue;

                                     var worker = new FileImporter(_ctx,
                                                                   _newFiles,
                                                                   _newDisks,
                                                                   _newMedias,
                                                                   KnownOnlyChecked,
                                                                   RemoveFilesChecked);

                                     worker.ImportAndHashRom(reader.OpenEntryStream(),
                                                             reader.Entry.Key,
                                                             Path.Combine(Settings.Settings.Current.RepositoryPath,
                                                                          Path.GetFileName(Path.GetTempFileName())),
                                                             reader.Entry.Size);
                                 }
                             }
                             catch(InvalidOperationException) {}
                             finally
                             {
                                 Interlocked.Increment(ref _listPosition);
                             }
                         });

        _stopwatch.Stop();
        Log.Debug("Took {TotalSeconds} seconds to process archives", _stopwatch.Elapsed.TotalSeconds);

        _ = InvokeAsync(() =>
        {
            Progress2Visible = false;

            StateHasChanged();
        });

        ProcessFiles();
    }
}