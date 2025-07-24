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
// Copyright © 2017-2024 Natalia Portillo
*******************************************************************************/

using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using System.Windows.Input;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.EntityFrameworkCore;
using MsBox.Avalonia;
using MsBox.Avalonia.Enums;
using RomRepoMgr.Core.EventArgs;
using RomRepoMgr.Core.Workers;
using RomRepoMgr.Database;
using RomRepoMgr.Resources;
using RomRepoMgr.Views;
using Serilog;
using Serilog.Extensions.Logging;
using ErrorEventArgs = RomRepoMgr.Core.EventArgs.ErrorEventArgs;

namespace RomRepoMgr.ViewModels;

public sealed partial class SettingsViewModel : ViewModelBase
{
    readonly SettingsDialog _view;
    bool                    _databaseChanged;
    string                  _databasePath;
    bool                    _repositoryChanged;
    string                  _repositoryPath;
    bool                    _temporaryChanged;
    string                  _temporaryPath;
    bool                    _unArChanged;
    [ObservableProperty]
    string _unArPath;
    [ObservableProperty]
    string _unArVersion;

    // Mock
    public SettingsViewModel() {}

    public SettingsViewModel(SettingsDialog view)
    {
        _view              = view;
        _databaseChanged   = false;
        _repositoryChanged = false;
        _temporaryChanged  = false;
        _unArChanged       = false;

        CloseCommand      = new RelayCommand(ExecuteCloseCommand);
        UnArCommand       = new AsyncRelayCommand(ExecuteUnArCommandAsync);
        TemporaryCommand  = new AsyncRelayCommand(ExecuteTemporaryCommandAsync);
        RepositoryCommand = new AsyncRelayCommand(ExecuteRepositoryCommandAsync);
        DatabaseCommand   = new AsyncRelayCommand(ExecuteDatabaseCommandAsync);
        SaveCommand       = new RelayCommand(ExecuteSaveCommand);

        DatabasePath   = Settings.Settings.Current.DatabasePath;
        RepositoryPath = Settings.Settings.Current.RepositoryPath;
        TemporaryPath  = Settings.Settings.Current.TemporaryFolder;
        UnArPath       = Settings.Settings.Current.UnArchiverPath;

        if(!string.IsNullOrWhiteSpace(UnArPath)) CheckUnAr();
    }

    public ICommand UnArCommand       { get; }
    public ICommand TemporaryCommand  { get; }
    public ICommand RepositoryCommand { get; }
    public ICommand DatabaseCommand   { get; }
    public ICommand CloseCommand      { get; }
    public ICommand SaveCommand       { get; }

    public string DatabasePath
    {
        get => _databasePath;
        set
        {
            SetProperty(ref _databasePath, value);
            _databaseChanged = true;
        }
    }

    public string RepositoryPath
    {
        get => _repositoryPath;
        set
        {
            SetProperty(ref _repositoryPath, value);

            // TODO: Refresh repository existing files
            _repositoryChanged = true;
        }
    }

    public string TemporaryPath
    {
        get => _temporaryPath;
        set
        {
            SetProperty(ref _temporaryPath, value);
            _temporaryChanged = true;
        }
    }

    void CheckUnAr()
    {
        var worker = new Compression();

        worker.FinishedWithText += CheckUnArFinished;
        worker.FailedWithText   += CheckUnArFailed;

        _ = Task.Run(() => worker.CheckUnAr(UnArPath));
    }

    void CheckUnArFailed(object sender, ErrorEventArgs args)
    {
        Dispatcher.UIThread.Post(() =>
        {
            UnArVersion = "";
            UnArPath    = "";

            _ = MessageBoxManager.GetMessageBoxStandard(Localization.Error, args.Message, ButtonEnum.Ok, Icon.Error)
                                 .ShowWindowDialogAsync(_view);
        });
    }

    void CheckUnArFinished(object sender, MessageEventArgs args) => Dispatcher.UIThread.Post(() =>
    {
        UnArVersion  = string.Format(Localization.TheUnarchiverVersionLabel, args.Message);
        _unArChanged = true;
    });

    void ExecuteCloseCommand() => _view.Close();

    async Task ExecuteUnArCommandAsync()
    {
        IReadOnlyList<IStorageFile> result = await _view.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title         = Localization.ChooseUnArExecutable,
            AllowMultiple = false,
            SuggestedStartLocation =
                !string.IsNullOrWhiteSpace(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles))
                    ? await _view.StorageProvider.TryGetFolderFromPathAsync(Environment.GetFolderPath(Environment
                                                                               .SpecialFolder.ProgramFiles))
                    : await _view.StorageProvider.TryGetWellKnownFolderAsync(WellKnownFolder.Desktop)
        });

        if(result.Count != 1) return;

        UnArPath = result[0].Path.LocalPath;
        CheckUnAr();
    }

    async Task ExecuteTemporaryCommandAsync()
    {
        IReadOnlyList<IStorageFolder> result =
            await _view.StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
            {
                Title = Localization.ChooseTemporaryFolder
            });

        if(result.Count < 1) return;

        TemporaryPath = result[0].Path.LocalPath;
    }

    async Task ExecuteRepositoryCommandAsync()
    {
        IReadOnlyList<IStorageFolder> result =
            await _view.StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
            {
                Title         = Localization.ChooseRepositoryFolder,
                AllowMultiple = false
            });

        if(result.Count < 1) return;

        RepositoryPath = result[0].Path.LocalPath;
    }

    async Task ExecuteDatabaseCommandAsync()
    {
        IStorageFile resultFile = await _view.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            SuggestedFileName      = "romrepo.db",
            SuggestedStartLocation = await _view.StorageProvider.TryGetWellKnownFolderAsync(WellKnownFolder.Documents),
            Title                  = Localization.ChooseDatabaseFile,
            ShowOverwritePrompt    = true
        });

        if(resultFile == null) return;

        string result = resultFile.Path.LocalPath;

        if(File.Exists(result))
        {
            ButtonResult btnResult = await MessageBoxManager
                                          .GetMessageBoxStandard(Localization.DatabaseFileExistsMsgBoxTitle,
                                                                 Localization.DatabaseFileTryOpenCaption,
                                                                 ButtonEnum.YesNo,
                                                                 Icon.Database)
                                          .ShowWindowDialogAsync(_view);

            if(btnResult == ButtonResult.Yes)
            {
                try
                {
                    var ctx = Context.Create(result, new SerilogLoggerFactory(Log.Logger));
                    await ctx.Database.MigrateAsync();
                }
                catch
                {
                    btnResult = await MessageBoxManager
                                     .GetMessageBoxStandard(Localization.DatabaseFileUnusableMsgBoxTitle,
                                                            Localization.DatabaseFileUnusableDeleteMsgBoxCaption,
                                                            ButtonEnum.YesNo,
                                                            Icon.Error)
                                     .ShowWindowDialogAsync(_view);

                    if(btnResult == ButtonResult.No) return;

                    try
                    {
                        File.Delete(result);
                    }
                    catch
                    {
                        await MessageBoxManager
                             .GetMessageBoxStandard(Localization.DatabaseFileCannotDeleteTitle,
                                                    Localization.DatabaseFileCannotDeleteCaption,
                                                    ButtonEnum.Ok,
                                                    Icon.Error)
                             .ShowWindowDialogAsync(_view);

#pragma warning disable ERP022
                        return;
#pragma warning restore ERP022
                    }
#pragma warning disable ERP022
                }
#pragma warning restore ERP022
            }
            else
            {
                btnResult = await MessageBoxManager
                                 .GetMessageBoxStandard(Localization.DatabaseFileExistsMsgBoxTitle,
                                                        Localization.DatabaseFileDeleteCaption,
                                                        ButtonEnum.YesNo,
                                                        Icon.Error)
                                 .ShowWindowDialogAsync(_view);

                if(btnResult == ButtonResult.No) return;

                try
                {
                    File.Delete(result);
                }
                catch
                {
                    await MessageBoxManager
                         .GetMessageBoxStandard(Localization.DatabaseFileCannotDeleteTitle,
                                                Localization.DatabaseFileCannotDeleteCaption,
                                                ButtonEnum.Ok,
                                                Icon.Error)
                         .ShowWindowDialogAsync(_view);

#pragma warning disable ERP022
                    return;
#pragma warning restore ERP022
                }
            }
        }

        try
        {
            var ctx = Context.Create(result, new SerilogLoggerFactory(Log.Logger));
            await ctx.Database.MigrateAsync();
        }
        catch
        {
            await MessageBoxManager
                 .GetMessageBoxStandard(Localization.DatabaseFileUnusableMsgBoxTitle,
                                        Localization.DatabaseFileUnusableMsgBoxCaption,
                                        ButtonEnum.Ok,
                                        Icon.Error)
                 .ShowWindowDialogAsync(_view);

#pragma warning disable ERP022
            return;
#pragma warning restore ERP022
        }

        DatabasePath = result;
    }

    void ExecuteSaveCommand()
    {
        if(_databaseChanged) Settings.Settings.Current.DatabasePath = DatabasePath;

        if(_repositoryChanged) Settings.Settings.Current.RepositoryPath = RepositoryPath;

        if(_temporaryChanged) Settings.Settings.Current.TemporaryFolder = TemporaryPath;

        if(_unArChanged)
        {
            Settings.Settings.Current.UnArchiverPath = UnArPath;
            Settings.Settings.UnArUsable             = true;
        }

        if(_databaseChanged || _repositoryChanged || _temporaryChanged || _unArChanged)
            Settings.Settings.SaveSettings();

        _view.Close();
    }
}