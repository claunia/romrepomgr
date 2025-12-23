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
// Copyright © 2020-2026 Natalia Portillo
*******************************************************************************/

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

    async Task ImportRomsAsync()
    {
        IDialogReference dialog = await DialogService.ShowDialogAsync<ImportRoms>(new DialogParameters());
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