using System.Diagnostics;
using Microsoft.AspNetCore.Components;
using Microsoft.FluentUI.AspNetCore.Components;
using RomRepoMgr.Core.Workers;
using Serilog;
using Serilog.Extensions.Logging;

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
    public FluentDialog Dialog { get;    set; }
    public int?   ProgressMax     { get; set; }
    public int?   ProgressMin     { get; set; }
    public int?   ProgressValue   { get; set; }
    public bool   CannotClose     { get; set; }
    public bool   ProgressVisible { get; set; }
    public Color? StatusColor     { get; set; }

    /// <inheritdoc />
    protected override void OnInitialized()
    {
        base.OnInitialized();

        path            = Path.Combine(Environment.CurrentDirectory, Consts.IncomingDatFolder);
        StatusMessage   = string.Empty;
        IsBusy          = false;
        CannotClose     = false;
        ProgressVisible = false;
    }

    Task StartAsync()
    {
        IsBusy          = true;
        CannotClose     = true;
        ProgressVisible = true;
        ProgressValue   = null;
        StatusMessage   = "Searching for files...";

        _stopwatch.Restart();
        string[] dats = Directory.GetFiles(path, "*.dat", SearchOption.AllDirectories);

        string[] xmls = Directory.GetFiles(path, "*.xml", SearchOption.AllDirectories);

        _datFiles = dats.Concat(xmls).Order().ToArray();
        _stopwatch.Stop();

        Logger.LogDebug("Took {TotalSeconds} to find {Length} DAT files",
                        _stopwatch.Elapsed.TotalSeconds,
                        _datFiles.Length);

        StatusMessage = string.Format("Found {0} files...", _datFiles.Length);

        ProgressMin   = 0;
        ProgressMax   = _datFiles.Length;
        ProgressValue = 0;
        _listPosition = 0;
        _workers      = 0;
        StateHasChanged();

        return ImportAsync();
    }

    async Task ImportAsync()
    {
        _stopwatch.Restart();
        Logger.LogDebug("Starting to import DAT files...");

        Parallel.ForEach(_datFiles,
                         datFile =>
                         {
                             _ = InvokeAsync(() =>
                             {
                                 StatusMessage = string.Format("Importing {0}...", Path.GetFileName(datFile));

                                 ProgressValue = _listPosition;
                                 StateHasChanged();
                             });

                             var worker = new DatImporter(datFile, null, new SerilogLoggerFactory(Log.Logger));

                             worker.Import();

                             Interlocked.Increment(ref _listPosition);
                         });

        ProgressVisible = false;
        StatusMessage   = "Finished";
        CannotClose     = false;
        _stopwatch.Stop();

        Logger.LogDebug("Took {TotalSeconds} seconds to import {Length} DAT files",
                        _stopwatch.Elapsed.TotalSeconds,
                        _datFiles.Length);

        StateHasChanged();
    }

    Task CloseAsync() => Dialog.CloseAsync();
}