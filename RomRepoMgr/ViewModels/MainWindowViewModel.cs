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
using System.Threading.Tasks;
using System.Windows.Input;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MsBox.Avalonia;
using MsBox.Avalonia.Enums;
using RomRepoMgr.Core.EventArgs;
using RomRepoMgr.Core.Filesystem;
using RomRepoMgr.Core.Models;
using RomRepoMgr.Resources;
using RomRepoMgr.Views;
using Serilog;
using Serilog.Extensions.Logging;

namespace RomRepoMgr.ViewModels;

public sealed partial class MainWindowViewModel : ViewModelBase
{
    readonly MainWindow _view;
    [ObservableProperty]
    RomSetModel _selectedRomSet;
    [ObservableProperty]
    Vfs _vfs;

    // Mock
    public MainWindowViewModel() {}

    public MainWindowViewModel(MainWindow view, List<RomSetModel> romSets)
    {
        _view                  = view;
        ExitCommand            = new RelayCommand(ExecuteExitCommand);
        SettingsCommand        = new AsyncRelayCommand(ExecuteSettingsCommandAsync);
        AboutCommand           = new RelayCommand(ExecuteAboutCommand);
        ImportDatCommand       = new AsyncRelayCommand(ExecuteImportDatCommandAsync);
        ImportDatFolderCommand = new AsyncRelayCommand(ExecuteImportDatFolderCommandAsync);
        ImportRomFolderCommand = new AsyncRelayCommand(ExecuteImportRomFolderCommandAsync);
        DeleteRomSetCommand    = new AsyncRelayCommand(ExecuteDeleteRomSetCommandAsync);
        EditRomSetCommand      = new RelayCommand(ExecuteEditRomSetCommand);
        ExportDatCommand       = new AsyncRelayCommand(ExecuteExportDatCommandAsync);
        ExportRomsCommand      = new AsyncRelayCommand(ExecuteExportRomsCommandAsync);
        MountCommand           = new AsyncRelayCommand(ExecuteMountCommandAsync);
        UmountCommand          = new RelayCommand(ExecuteUmountCommand);
        UpdateStatsCommand     = new AsyncRelayCommand(ExecuteUpdateStatsCommandAsync);
        RomSets                = new ObservableCollection<RomSetModel>(romSets);
    }

    public ObservableCollection<RomSetModel> RomSets        { get; }
    public bool                              IsVfsAvailable => Vfs.IsAvailable;

    public bool NativeMenuSupported =>
        NativeMenu.GetIsNativeMenuExported((Application.Current.ApplicationLifetime as
                                                IClassicDesktopStyleApplicationLifetime)?.MainWindow);

    public ICommand AboutCommand           { get; }
    public ICommand ExitCommand            { get; }
    public ICommand SettingsCommand        { get; }
    public ICommand ImportDatCommand       { get; }
    public ICommand ImportDatFolderCommand { get; }
    public ICommand ImportRomFolderCommand { get; }
    public ICommand DeleteRomSetCommand    { get; }
    public ICommand EditRomSetCommand      { get; }
    public ICommand ExportDatCommand       { get; }
    public ICommand ExportRomsCommand      { get; }
    public ICommand MountCommand           { get; }
    public ICommand UmountCommand          { get; }
    public ICommand UpdateStatsCommand     { get; }

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
            Vfs          =  new Vfs(new SerilogLoggerFactory(Log.Logger));
            Vfs.Umounted += VfsOnUmounted;
            Vfs.MountTo(result[0].Path.LocalPath);
        }
        catch(Exception ex)
        {
            Log.Error(ex, "Error mounting VFS");

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