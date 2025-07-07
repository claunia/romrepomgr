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
// Copyright © 2020-2024 Natalia Portillo
*******************************************************************************/

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Reactive;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Threading;
using MsBox.Avalonia;
using MsBox.Avalonia.Enums;
using ReactiveUI;
using RomRepoMgr.Core.EventArgs;
using RomRepoMgr.Core.Filesystem;
using RomRepoMgr.Core.Models;
using RomRepoMgr.Resources;
using RomRepoMgr.Views;

namespace RomRepoMgr.ViewModels;

public class MainWindowViewModel : ViewModelBase
{
    readonly MainWindow _view;
    RomSetModel         _selectedRomSet;
    Vfs                 _vfs;

    public MainWindowViewModel(MainWindow view, List<RomSetModel> romSets)
    {
        _view                  = view;
        ExitCommand            = ReactiveCommand.Create(ExecuteExitCommand);
        SettingsCommand        = ReactiveCommand.Create(ExecuteSettingsCommand);
        AboutCommand           = ReactiveCommand.Create(ExecuteAboutCommand);
        ImportDatCommand       = ReactiveCommand.Create(ExecuteImportDatCommand);
        ImportDatFolderCommand = ReactiveCommand.Create(ExecuteImportDatFolderCommand);
        ImportRomFolderCommand = ReactiveCommand.Create(ExecuteImportRomFolderCommand);
        DeleteRomSetCommand    = ReactiveCommand.Create(ExecuteDeleteRomSetCommand);
        EditRomSetCommand      = ReactiveCommand.Create(ExecuteEditRomSetCommand);
        ExportDatCommand       = ReactiveCommand.Create(ExecuteExportDatCommand);
        ExportRomsCommand      = ReactiveCommand.Create(ExecuteExportRomsCommand);
        MountCommand           = ReactiveCommand.Create(ExecuteMountCommand);
        UmountCommand          = ReactiveCommand.Create(ExecuteUmountCommand);
        UpdateStatsCommand     = ReactiveCommand.Create(ExecuteUpdateStatsCommand);
        RomSets                = new ObservableCollection<RomSetModel>(romSets);
    }

    public ObservableCollection<RomSetModel> RomSets { get; }
    public string RomSetLabel => Localization.RomSets;
    public string RomSetNameLabel => Localization.RomSetNameLabel;
    public string RomSetVersionLabel => Localization.RomSetVersionLabel;
    public string RomSetAuthorLabel => Localization.RomSetAuthorLabel;
    public string RomSetCategoryLabel => Localization.RomSetCategoryLabel;
    public string RomSetDateLabel => Localization.RomSetDateLabel;
    public string RomSetDescriptionLabel => Localization.RomSetDescriptionLabel;
    public string RomSetCommentLabel => Localization.RomSetCommentLabel;
    public string RomSetTotalMachinesLabel => Localization.RomSetTotalMachinesLabel;
    public string RomSetCompleteMachinesLabel => Localization.RomSetCompleteMachinesLabel;
    public string RomSetIncompleteMachinesLabel => Localization.RomSetIncompleteMachinesLabel;
    public string RomSetTotalRomsLabel => Localization.RomSetTotalRomsLabel;
    public string RomSetHaveRomsLabel => Localization.RomSetHaveRomsLabel;
    public string RomSetMissRomsLabel => Localization.RomSetMissRomsLabel;
    public bool IsVfsAvailable => Vfs.IsAvailable;
    public string FileMenuText => Localization.FileMenuText;
    public string FileMenuImportDatFileText => Localization.FileMenuImportDatFileText;
    public string FileMenuImportDatFolderText => Localization.FileMenuImportDatFolderText;
    public string FileMenuSettingsText => Localization.FileMenuSettingsText;
    public string FileMenuExitText => Localization.FileMenuExitText;
    public string FilesystemMenuText => Localization.FilesystemMenuText;
    public string FilesystemMenuMountText => Localization.FilesystemMenuMountText;
    public string RomsMenuText => Localization.RomsMenuText;
    public string RomsMenuImportText => Localization.RomsMenuImportText;
    public string RomSetsMenuText => Localization.RomSetsMenuText;
    public string RomSetsMenuSaveRomsText => Localization.RomSetsMenuSaveRomsText;
    public string RomSetsMenuSaveDatText => Localization.RomSetsMenuSaveDatText;
    public string RomSetsMenuEditText => Localization.RomSetsMenuEditText;
    public string RomSetsMenuDeleteText => Localization.RomSetsMenuDeleteText;
    public string HelpMenuText => Localization.HelpMenuText;
    public string HelpMenuAboutText => Localization.HelpMenuAboutText;
    public string FilesystemMenuUmountText => Localization.FilesystemMenuUmountText;
    public string DatabaseMenuText => Localization.DatabaseMenuText;
    public string DatabaseMenuUpdateStatsText => Localization.DatabaseMenuUpdateStatsText;

    public bool NativeMenuSupported =>
        NativeMenu.GetIsNativeMenuExported((Application.Current.ApplicationLifetime as
                                                IClassicDesktopStyleApplicationLifetime)?.MainWindow);

    public ReactiveCommand<Unit, Unit> AboutCommand           { get; }
    public ReactiveCommand<Unit, Unit> ExitCommand            { get; }
    public ReactiveCommand<Unit, Unit> SettingsCommand        { get; }
    public ReactiveCommand<Unit, Unit> ImportDatCommand       { get; }
    public ReactiveCommand<Unit, Unit> ImportDatFolderCommand { get; }
    public ReactiveCommand<Unit, Unit> ImportRomFolderCommand { get; }
    public ReactiveCommand<Unit, Unit> DeleteRomSetCommand    { get; }
    public ReactiveCommand<Unit, Unit> EditRomSetCommand      { get; }
    public ReactiveCommand<Unit, Unit> ExportDatCommand       { get; }
    public ReactiveCommand<Unit, Unit> ExportRomsCommand      { get; }
    public ReactiveCommand<Unit, Unit> MountCommand           { get; }
    public ReactiveCommand<Unit, Unit> UmountCommand          { get; }
    public ReactiveCommand<Unit, Unit> UpdateStatsCommand     { get; }

    public Vfs Vfs
    {
        get => _vfs;
        set => this.RaiseAndSetIfChanged(ref _vfs, value);
    }

    public RomSetModel SelectedRomSet
    {
        get => _selectedRomSet;
        set => this.RaiseAndSetIfChanged(ref _selectedRomSet, value);
    }

    internal async void ExecuteSettingsCommand()
    {
        var dialog = new SettingsDialog();
        dialog.DataContext = new SettingsViewModel(dialog);
        await dialog.ShowDialog(_view);
    }

    internal void ExecuteExitCommand() =>
        (Application.Current.ApplicationLifetime as ClassicDesktopStyleApplicationLifetime)?.Shutdown();

    internal void ExecuteAboutCommand()
    {
        var dialog = new About();
        dialog.DataContext = new AboutViewModel(dialog);
        _                  = dialog.ShowDialog(_view);
    }

    async void ExecuteImportDatCommand()
    {
        var dlgOpen = new OpenFileDialog
        {
            AllowMultiple = false,
            Title         = Localization.ImportDatFileDialogTitle
        };

        dlgOpen.Filters.Add(new FileDialogFilter
        {
            Extensions = ["dat", "xml"],
            Name       = Localization.DatFilesDialogLabel
        });

        dlgOpen.Filters.Add(new FileDialogFilter
        {
            Extensions = ["*"],
            Name       = Localization.AllFilesDialogLabel
        });

        string[] result = await dlgOpen.ShowAsync(_view);

        if(result?.Length != 1) return;

        var dialog             = new ImportDat();
        var importDatViewModel = new ImportDatViewModel(dialog, result[0]);
        importDatViewModel.RomSetAdded += ImportDatViewModelOnRomSetAdded;
        dialog.DataContext             =  importDatViewModel;
        await dialog.ShowDialog(_view);
    }

    void ImportDatViewModelOnRomSetAdded(object sender, RomSetEventArgs e) =>
        Dispatcher.UIThread.Post(() => RomSets.Add(e.RomSet));

    async void ExecuteImportDatFolderCommand()
    {
        var dlgOpen = new OpenFolderDialog
        {
            Title = Localization.ImportDatFolderDialogTitle
        };

        string result = await dlgOpen.ShowAsync(_view);

        if(result == null) return;

        var dialog                   = new ImportDatFolder();
        var importDatFolderViewModel = new ImportDatFolderViewModel(dialog, result);
        importDatFolderViewModel.RomSetAdded += ImportDatViewModelOnRomSetAdded;
        dialog.DataContext                   =  importDatFolderViewModel;
        await dialog.ShowDialog(_view);
    }

    async void ExecuteImportRomFolderCommand()
    {
        var dlgOpen = new OpenFolderDialog
        {
            Title = Localization.ImportRomsFolderDialogTitle
        };

        string result = await dlgOpen.ShowAsync(_view);

        if(result == null) return;

        var dialog                   = new ImportRomFolder();
        var importRomFolderViewModel = new ImportRomFolderViewModel(dialog, result);
        dialog.DataContext = importRomFolderViewModel;
        await dialog.ShowDialog(_view);
    }

    async void ExecuteDeleteRomSetCommand()
    {
        if(SelectedRomSet == null) return;

        ButtonResult result = await MessageBoxManager
                                   .GetMessageBoxStandard(Localization.DeleteRomSetMsgBoxTitle,
                                                          string.Format(Localization.DeleteRomSetMsgBoxCaption,
                                                                        SelectedRomSet.Name),
                                                          ButtonEnum.YesNo,
                                                          Icon.Database)
                                   .ShowWindowDialogAsync(_view);

        if(result == ButtonResult.No) return;

        var dialog             = new RemoveDat();
        var removeDatViewModel = new RemoveDatViewModel(dialog, SelectedRomSet.Id);
        dialog.DataContext = removeDatViewModel;
        await dialog.ShowDialog(_view);

        RomSets.Remove(SelectedRomSet);
        SelectedRomSet = null;
    }

    void ExecuteEditRomSetCommand()
    {
        if(SelectedRomSet == null) return;

        var window    = new EditDat();
        var viewModel = new EditDatViewModel(window, SelectedRomSet);

        viewModel.RomSetModified += (sender, args) =>
        {
            RomSetModel old = RomSets.FirstOrDefault(r => r.Id == args.RomSet.Id);

            if(old == null) return;

            RomSets.Remove(old);
            RomSets.Add(args.RomSet);
        };

        window.DataContext = viewModel;

        window.Show();
    }

    async void ExecuteExportDatCommand()
    {
        if(SelectedRomSet == null) return;

        var dlgSave = new SaveFileDialog
        {
            InitialFileName = SelectedRomSet.Filename
        };

        string result = await dlgSave.ShowAsync(_view);

        if(result == null) return;

        var dialog    = new ExportDat();
        var viewModel = new ExportDatViewModel(dialog, SelectedRomSet.Sha384, result);
        dialog.DataContext = viewModel;
        await dialog.ShowDialog(_view);
    }

    async void ExecuteExportRomsCommand()
    {
        var dlgOpen = new OpenFolderDialog
        {
            Title = Localization.ExportRomsDialogTitle
        };

        string result = await dlgOpen.ShowAsync(_view);

        if(result == null) return;

        var dialog    = new ExportRoms();
        var viewModel = new ExportRomsViewModel(dialog, result, SelectedRomSet.Id);
        dialog.DataContext = viewModel;
        await dialog.ShowDialog(_view);
    }

    async void ExecuteMountCommand()
    {
        if(Vfs != null) return;

        var dlgOpen = new OpenFolderDialog
        {
            Title = Localization.SelectMountPointDialogTitle
        };

        string result = await dlgOpen.ShowAsync(_view);

        if(result == null) return;

        try
        {
            Vfs          =  new Vfs();
            Vfs.Umounted += VfsOnUmounted;
            Vfs.MountTo(result);
        }
        catch(Exception)
        {
            if(Debugger.IsAttached) throw;

            Vfs = null;
        }
    }

    void VfsOnUmounted(object sender, EventArgs e) => Vfs = null;

    void ExecuteUmountCommand() => Vfs?.Umount();

    async void ExecuteUpdateStatsCommand()
    {
        ButtonResult result = await MessageBoxManager
                                   .GetMessageBoxStandard(Localization.DatabaseMenuUpdateStatsText,
                                                          Localization.UpdateStatsConfirmationDialogText,
                                                          ButtonEnum.YesNo,
                                                          Icon.Database)
                                   .ShowWindowDialogAsync(_view);

        if(result == ButtonResult.No) return;

        var view      = new UpdateStats();
        var viewModel = new UpdateStatsViewModel(view);
        view.DataContext = viewModel;
        await view.ShowDialog(_view);
    }
}