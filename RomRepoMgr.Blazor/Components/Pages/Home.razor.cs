using Microsoft.AspNetCore.Components;
using Microsoft.FluentUI.AspNetCore.Components;
using RomRepoMgr.Blazor.Components.Dialogs;

namespace RomRepoMgr.Blazor.Components.Pages;

public partial class Home : ComponentBase
{
    async Task ImportDatsAsync()
    {
        IDialogReference dialog = await DialogService.ShowDialogAsync<ImportDats>(new DialogParameters());
    }
}