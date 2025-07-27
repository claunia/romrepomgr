using Microsoft.AspNetCore.Components;
using Microsoft.FluentUI.AspNetCore.Components;
using RomRepoMgr.Blazor.Components.Dialogs;
using RomRepoMgr.Core.Models;

namespace RomRepoMgr.Blazor.Components.Pages;

public partial class Home : ComponentBase
{
    readonly PaginationState pagination = new()
    {
        ItemsPerPage = 10
    };
    FluentDataGrid<RomSetModel>? romSetsGrid;

    public IQueryable<RomSetModel>? RomSets { get; set; }

    async Task ImportDatsAsync()
    {
        IDialogReference dialog = await DialogService.ShowDialogAsync<ImportDats>(new DialogParameters());
    }

    /// <inheritdoc />
    protected override void OnInitialized()
    {
        base.OnInitialized();

        romSetsGrid?.SetLoadingState(true);

        RomSets = ctx.RomSets.OrderBy(r => r.Name)
                     .ThenBy(r => r.Version)
                     .ThenBy(r => r.Date)
                     .ThenBy(r => r.Description)
                     .ThenBy(r => r.Comment)
                     .ThenBy(r => r.Filename)
                     .Select(r => new RomSetModel
                      {
                          Id                 = r.Id,
                          Author             = r.Author,
                          Comment            = r.Comment,
                          Date               = r.Date,
                          Description        = r.Description,
                          Filename           = r.Filename,
                          Homepage           = r.Homepage,
                          Name               = r.Name,
                          Sha384             = r.Sha384,
                          Version            = r.Version,
                          TotalMachines      = r.Statistics.TotalMachines,
                          CompleteMachines   = r.Statistics.CompleteMachines,
                          IncompleteMachines = r.Statistics.IncompleteMachines,
                          TotalRoms          = r.Statistics.TotalRoms,
                          HaveRoms           = r.Statistics.HaveRoms,
                          MissRoms           = r.Statistics.MissRoms,
                          Category           = r.Category
                      });

        romSetsGrid?.SetLoadingState(false);
    }
}