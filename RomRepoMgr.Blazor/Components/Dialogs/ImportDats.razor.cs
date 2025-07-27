using System.Diagnostics;
using Microsoft.AspNetCore.Components;
using RomRepoMgr.Core.EventArgs;
using RomRepoMgr.Core.Workers;
using Serilog;
using Serilog.Extensions.Logging;
using ErrorEventArgs = RomRepoMgr.Core.EventArgs.ErrorEventArgs;

namespace RomRepoMgr.Blazor.Components.Dialogs;

public partial class ImportDats : ComponentBase
{
    readonly Stopwatch _stopwatch = new();
    string[]           _datFiles;
    int                _listPosition;
    int                _workers;
    string             path;
    public string      StatusMessage { get; set; }
    public bool        IsBusy        { get; set; }
    [CascadingParameter]
    public FluentDialog Dialog { get;     set; }
    public int?   ProgressMax      { get; set; }
    public int?   ProgressMin      { get; set; }
    public int?   ProgressValue    { get; set; }
    public bool   CannotClose      { get; set; }
    public bool   ProgressVisible  { get; set; }
    public string StatusMessage2   { get; set; }
    public bool   Progress2Visible { get; set; }
    public int?   Progress2Max     { get; set; }
    public int?   Progress2Min     { get; set; }
    public int?   Progress2Value   { get; set; }
    public Color? StatusColor      { get; set; }

    /// <inheritdoc />
    protected override void OnInitialized()
    {
        base.OnInitialized();

        path             = Configuration["DataFolders:ImportDats"] ?? "incoming-dats";
        StatusMessage    = "";
        StatusMessage2   = "";
        IsBusy           = false;
        CannotClose      = false;
        ProgressVisible  = false;
        Progress2Visible = false;
    }

    void Start()
    {
        IsBusy          = true;
        CannotClose     = true;
        ProgressVisible = true;
        ProgressValue   = null;
        StatusMessage   = Localizer["SearchingForFiles"];

        _stopwatch.Restart();
        string[] dats = Directory.GetFiles(path, "*.dat", SearchOption.AllDirectories);

        string[] xmls = Directory.GetFiles(path, "*.xml", SearchOption.AllDirectories);

        _datFiles = dats.Concat(xmls).Order().ToArray();
        _stopwatch.Stop();

        Logger.LogDebug("Took {TotalSeconds} to find {Length} DAT files",
                        _stopwatch.Elapsed.TotalSeconds,
                        _datFiles.Length);

        StatusMessage = string.Format(Localizer["FoundFiles"], _datFiles.Length);

        ProgressMin      = 0;
        ProgressMax      = _datFiles.Length;
        ProgressValue    = 0;
        Progress2Visible = true;
        _listPosition    = 0;
        _workers         = 0;
        StateHasChanged();

        _stopwatch.Restart();
        Logger.LogDebug("Starting to import DAT files...");
        Import();
    }

    void Import()
    {
        if(_listPosition >= _datFiles.Length)
        {
            _ = InvokeAsync(() =>
            {
                ProgressVisible  = false;
                Progress2Visible = false;
                StatusMessage    = Localizer["Finished"];
                CannotClose      = false;

                StateHasChanged();
            });

            _stopwatch.Stop();

            Logger.LogDebug("Took {TotalSeconds} seconds to import {Length} DAT files",
                            _stopwatch.Elapsed.TotalSeconds,
                            _datFiles.Length);


            return;
        }

        _ = InvokeAsync(() =>
        {
            StatusMessage = string.Format(Localizer["ImportingItem"], Path.GetFileName(_datFiles[_listPosition]));
            ProgressValue = _listPosition;

            StateHasChanged();
        });

        var worker = new DatImporter(_datFiles[_listPosition], null, new SerilogLoggerFactory(Log.Logger));

        worker.ErrorOccurred            += OnWorkerOnErrorOccurred;
        worker.SetIndeterminateProgress += OnWorkerOnSetIndeterminateProgress;
        worker.SetMessage               += OnWorkerOnSetMessage;
        worker.SetProgress              += OnWorkerOnSetProgress;
        worker.SetProgressBounds        += OnWorkerOnSetProgressBounds;
        worker.WorkFinished             += OnWorkerOnWorkFinished;
        _                               =  Task.Run(worker.Import);
    }

    void OnWorkerOnWorkFinished(object? sender, MessageEventArgs args)
    {
        _listPosition++;
        Import();
    }

    void OnWorkerOnSetProgressBounds(object? sender, ProgressBoundsEventArgs args)
    {
        _ = InvokeAsync(() =>
        {
            Progress2Value = 0;
            Progress2Max   = (int?)args.Maximum;
            Progress2Min   = (int?)args.Minimum;
            StateHasChanged();
        });
    }

    void OnWorkerOnSetProgress(object? sender, ProgressEventArgs args)
    {
        _ = InvokeAsync(() =>
        {
            Progress2Value = (int?)args.Value;
            StateHasChanged();
        });
    }

    void OnWorkerOnSetMessage(object? sender, MessageEventArgs args)
    {
        _ = InvokeAsync(() =>
        {
            StatusMessage2 = args.Message;
            StateHasChanged();
        });
    }

    void OnWorkerOnSetIndeterminateProgress(object? sender, EventArgs args)
    {
        _ = InvokeAsync(() =>
        {
            Progress2Value = null;
            StateHasChanged();
        });
    }

    void OnWorkerOnErrorOccurred(object? sender, ErrorEventArgs args)
    {
        _ = InvokeAsync(() =>
        {
            _listPosition++;
            Import();
        });
    }

    Task CloseAsync() => Dialog.CloseAsync();
}