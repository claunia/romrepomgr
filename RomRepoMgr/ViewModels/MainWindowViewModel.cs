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
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Platform.Storage;
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

    // Mock
    public MainWindowViewModel() {}

    public MainWindowViewModel(MainWindow view, List<RomSetModel> romSets)
    {
        _view                  = view;
        ExitCommand            = ReactiveCommand.Create(ExecuteExitCommand);
        SettingsCommand        = ReactiveCommand.CreateFromTask(ExecuteSettingsCommandAsync);
        AboutCommand           = ReactiveCommand.Create(ExecuteAboutCommand);
        ImportDatCommand       = ReactiveCommand.CreateFromTask(ExecuteImportDatCommandAsync);
        ImportDatFolderCommand = ReactiveCommand.CreateFromTask(ExecuteImportDatFolderCommandAsync);
        ImportRomFolderCommand = ReactiveCommand.CreateFromTask(ExecuteImportRomFolderCommandAsync);
        DeleteRomSetCommand    = ReactiveCommand.CreateFromTask(ExecuteDeleteRomSetCommandAsync);
        EditRomSetCommand      = ReactiveCommand.Create(ExecuteEditRomSetCommand);
        ExportDatCommand       = ReactiveCommand.CreateFromTask(ExecuteExportDatCommandAsync);
        ExportRomsCommand      = ReactiveCommand.CreateFromTask(ExecuteExportRomsCommandAsync);
        MountCommand           = ReactiveCommand.CreateFromTask(ExecuteMountCommandAsync);
        UmountCommand          = ReactiveCommand.Create(ExecuteUmountCommand);
        UpdateStatsCommand     = ReactiveCommand.CreateFromTask(ExecuteUpdateStatsCommandAsync);
        RomSets                = new ObservableCollection<RomSetModel>(romSets);
    }

    public ObservableCollection<RomSetModel> RomSets        { get; }
    public bool                              IsVfsAvailable => Vfs.IsAvailable;

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

    internal Task ExecuteSettingsCommandAsync()
    {
        var dialog = new SettingsDialog();
        dialog.DataContext = new SettingsViewModel(dialog);

        return dialog.ShowDialog(_view);
    }

    internal void ExecuteExitCommand() =>
        (Application.Current.ApplicationLifetime as ClassicDesktopStyleApplicationLifetime)?.Shutdown();

    internal void ExecuteAboutCommand()
    {
        var dialog = new About();
        dialog.DataContext = new AboutViewModel(dialog);
        _                  = dialog.ShowDialog(_view);
    }

    async Task ExecuteImportDatCommandAsync()
    {
        var datFileType = new FilePickerFileType(Localization.DatFilesDialogLabel)
        {
            Patterns                    = ["*.dat", "*.xml"],
            AppleUniformTypeIdentifiers = ["public.xml", "public.json"],
            MimeTypes                   = ["application/xml", "text/*"]
        };

        IReadOnlyList<IStorageFile> result = await _view.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title                  = Localization.ImportDatFileDialogTitle,
            AllowMultiple          = false,
            SuggestedStartLocation = await _view.StorageProvider.TryGetWellKnownFolderAsync(WellKnownFolder.Documents),
            FileTypeFilter         = [datFileType, FilePickerFileTypes.All]
        });

        if(result.Count != 1) return;

        var dialog             = new ImportDat();
        var importDatViewModel = new ImportDatViewModel(dialog, result[0].Path.LocalPath);
        importDatViewModel.RomSetAdded += ImportDatViewModelOnRomSetAdded;
        dialog.DataContext             =  importDatViewModel;
        _                              =  dialog.ShowDialog(_view);
    }

    void ImportDatViewModelOnRomSetAdded(object sender, RomSetEventArgs e) =>
        Dispatcher.UIThread.Post(() => RomSets.Add(e.RomSet));

    async Task ExecuteImportDatFolderCommandAsync()
    {
        var dialog = new ImportDatFolder();

        var viewModel = new ImportDatFolderViewModel
        {
            View = dialog
        };

        viewModel.RomSetAdded += ImportDatViewModelOnRomSetAdded;

        dialog.DataContext = viewModel;
        _                  = dialog.ShowDialog(_view);
    }

    async Task ExecuteImportRomFolderCommandAsync()
    {
        var dialog = new ImportRomFolder();

        var viewModel = new ImportRomFolderViewModel
        {
            View = dialog
        };

        dialog.DataContext = viewModel;
        _                  = dialog.ShowDialog(_view);
    }

    async Task ExecuteDeleteRomSetCommandAsync()
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

        viewModel.RomSetModified += (_, args) =>
        {
            RomSetModel old = RomSets.FirstOrDefault(r => r.Id == args.RomSet.Id);

            if(old == null) return;

            RomSets.Remove(old);
            RomSets.Add(args.RomSet);
        };

        window.DataContext = viewModel;

        window.Show();
    }

    async Task ExecuteExportDatCommandAsync()
    {
        if(SelectedRomSet == null) return;

        IStorageFile result = await _view.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            SuggestedFileName      = SelectedRomSet.Filename,
            SuggestedStartLocation = await _view.StorageProvider.TryGetWellKnownFolderAsync(WellKnownFolder.Documents)
        });

        if(result == null) return;

        var dialog    = new ExportDat();
        var viewModel = new ExportDatViewModel(dialog, SelectedRomSet.Sha384, result.Path.LocalPath);
        dialog.DataContext = viewModel;
        _                  = dialog.ShowDialog(_view);
    }

    async Task ExecuteExportRomsCommandAsync()
    {
        IReadOnlyList<IStorageFolder> result =
            await _view.StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
            {
                Title = Localization.ExportRomsDialogTitle
            });

        if(result.Count < 1) return;

        var dialog    = new ExportRoms();
        var viewModel = new ExportRomsViewModel(dialog, result[0].Path.LocalPath, SelectedRomSet.Id);
        dialog.DataContext = viewModel;
        _                  = dialog.ShowDialog(_view);
    }

    async Task ExecuteMountCommandAsync()
    {
        if(Vfs != null) return;

        IReadOnlyList<IStorageFolder> result =
            await _view.StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
            {
                Title = Localization.SelectMountPointDialogTitle
            });

        if(result.Count < 1) return;

        try
        {
            Vfs          =  new Vfs();
            Vfs.Umounted += VfsOnUmounted;
            Vfs.MountTo(result[0].Path.LocalPath);
        }
        catch(Exception)
        {
            if(Debugger.IsAttached) throw;

            Vfs = null;
        }
    }

    void VfsOnUmounted(object sender, EventArgs e) => Vfs = null;

    void ExecuteUmountCommand() => Vfs?.Umount();

    async Task ExecuteUpdateStatsCommandAsync()
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
        _                = view.ShowDialog(_view);
    }
}