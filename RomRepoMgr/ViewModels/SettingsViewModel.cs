﻿/******************************************************************************
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
// Copyright © 2017-2021 Natalia Portillo
*******************************************************************************/

using System;
using System.IO;
using System.Reactive;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Threading;
using JetBrains.Annotations;
using MessageBox.Avalonia;
using MessageBox.Avalonia.Enums;
using Microsoft.EntityFrameworkCore;
using ReactiveUI;
using RomRepoMgr.Core.EventArgs;
using RomRepoMgr.Core.Workers;
using RomRepoMgr.Database;
using RomRepoMgr.Resources;
using RomRepoMgr.Views;
using ErrorEventArgs = RomRepoMgr.Core.EventArgs.ErrorEventArgs;

namespace RomRepoMgr.ViewModels
{
    public sealed class SettingsViewModel : ViewModelBase
    {
        readonly SettingsDialog _view;
        bool                    _databaseChanged;
        string                  _databasePath;
        bool                    _repositoryChanged;
        string                  _repositoryPath;
        bool                    _temporaryChanged;
        string                  _temporaryPath;
        bool                    _unArChanged;
        string                  _unArPath;
        string                  _unArVersion;

        public SettingsViewModel(SettingsDialog view)
        {
            _view              = view;
            _databaseChanged   = false;
            _repositoryChanged = false;
            _temporaryChanged  = false;
            _unArChanged       = false;

            CloseCommand      = ReactiveCommand.Create(ExecuteCloseCommand);
            UnArCommand       = ReactiveCommand.Create(ExecuteUnArCommand);
            TemporaryCommand  = ReactiveCommand.Create(ExecuteTemporaryCommand);
            RepositoryCommand = ReactiveCommand.Create(ExecuteRepositoryCommand);
            DatabaseCommand   = ReactiveCommand.Create(ExecuteDatabaseCommand);
            SaveCommand       = ReactiveCommand.Create(ExecuteSaveCommand);

            DatabasePath   = Settings.Settings.Current.DatabasePath;
            RepositoryPath = Settings.Settings.Current.RepositoryPath;
            TemporaryPath  = Settings.Settings.Current.TemporaryFolder;
            UnArPath       = Settings.Settings.Current.UnArchiverPath;

            if(!string.IsNullOrWhiteSpace(UnArPath))
                CheckUnAr();
        }

        public string ChooseLabel     => Localization.ChooseLabel;
        public string Title           => Localization.SettingsTitle;
        public string CloseLabel      => Localization.CloseLabel;
        public string DatabaseLabel   => Localization.DatabaseFileLabel;
        public string RepositoryLabel => Localization.RepositoryFolderLabel;
        public string TemporaryLabel  => Localization.TemporaryFolderLabel;
        public string UnArPathLabel   => Localization.UnArPathLabel;

        public ReactiveCommand<Unit, Unit> UnArCommand       { get; }
        public ReactiveCommand<Unit, Unit> TemporaryCommand  { get; }
        public ReactiveCommand<Unit, Unit> RepositoryCommand { get; }
        public ReactiveCommand<Unit, Unit> DatabaseCommand   { get; }
        public ReactiveCommand<Unit, Unit> CloseCommand      { get; }
        public ReactiveCommand<Unit, Unit> SaveCommand       { get; }

        public string DatabasePath
        {
            get => _databasePath;
            set
            {
                this.RaiseAndSetIfChanged(ref _databasePath, value);
                _databaseChanged = true;
            }
        }

        public string RepositoryPath
        {
            get => _repositoryPath;
            set
            {
                this.RaiseAndSetIfChanged(ref _repositoryPath, value);

                // TODO: Refresh repository existing files
                _repositoryChanged = true;
            }
        }

        public string TemporaryPath
        {
            get => _temporaryPath;
            set
            {
                this.RaiseAndSetIfChanged(ref _temporaryPath, value);
                _temporaryChanged = true;
            }
        }

        public string UnArPath
        {
            get => _unArPath;
            set => this.RaiseAndSetIfChanged(ref _unArPath, value);
        }

        public string UnArVersion
        {
            get => _unArVersion;
            set => this.RaiseAndSetIfChanged(ref _unArVersion, value);
        }

        public string SaveLabel => Localization.SaveLabel;

        void CheckUnAr()
        {
            var worker = new Compression();

            worker.FinishedWithText += CheckUnArFinished;
            worker.FailedWithText   += CheckUnArFailed;

            Task.Run(() => worker.CheckUnAr(UnArPath));
        }

        async void CheckUnArFailed(object sender, [NotNull] ErrorEventArgs args)
        {
            UnArVersion = "";
            UnArPath    = "";

            await MessageBoxManager.
                  GetMessageBoxStandardWindow(Localization.Error, $"{args.Message}", ButtonEnum.Ok, Icon.Error).
                  ShowDialog(_view);
        }

        void CheckUnArFinished(object sender, MessageEventArgs args) => Dispatcher.UIThread.Post(() =>
        {
            UnArVersion  = string.Format(Localization.TheUnarchiverVersionLabel, args.Message);
            _unArChanged = true;
        });

        void ExecuteCloseCommand() => _view.Close();

        async void ExecuteUnArCommand()
        {
            var dlgFile = new OpenFileDialog
            {
                Title         = Localization.ChooseUnArExecutable,
                AllowMultiple = false
            };

            if(!string.IsNullOrWhiteSpace(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles)))
                dlgFile.Directory = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);

            string[] result = await dlgFile.ShowAsync(_view);

            if(result?.Length != 1)
                return;

            UnArPath = result[0];
            CheckUnAr();
        }

        async void ExecuteTemporaryCommand()
        {
            var dlgFolder = new OpenFolderDialog
            {
                Title = Localization.ChooseTemporaryFolder
            };

            string result = await dlgFolder.ShowAsync(_view);

            if(result == null)
                return;

            TemporaryPath = result;
        }

        async void ExecuteRepositoryCommand()
        {
            var dlgFolder = new OpenFolderDialog
            {
                Title = Localization.ChooseRepositoryFolder
            };

            string result = await dlgFolder.ShowAsync(_view);

            if(result == null)
                return;

            RepositoryPath = result;
        }

        async void ExecuteDatabaseCommand()
        {
            var dlgFile = new SaveFileDialog
            {
                InitialFileName = "romrepo.db",
                Directory       = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                Title           = Localization.ChooseDatabaseFile
            };

            string result = await dlgFile.ShowAsync(_view);

            if(result == null)
                return;

            if(File.Exists(result))
            {
                ButtonResult btnResult = await MessageBoxManager.
                                               GetMessageBoxStandardWindow(Localization.DatabaseFileExistsMsgBoxTitle,
                                                                           Localization.DatabaseFileTryOpenCaption,
                                                                           ButtonEnum.YesNo, Icon.Database).
                                               ShowDialog(_view);

                if(btnResult == ButtonResult.Yes)
                {
                    try
                    {
                        var ctx = Context.Create(result);
                        await ctx.Database.MigrateAsync();
                    }
                    catch(Exception)
                    {
                        btnResult = await MessageBoxManager.
                                          GetMessageBoxStandardWindow(Localization.DatabaseFileUnusableMsgBoxTitle,
                                                                      Localization.
                                                                          DatabaseFileUnusableDeleteMsgBoxCaption,
                                                                      ButtonEnum.YesNo, Icon.Error).ShowDialog(_view);

                        if(btnResult == ButtonResult.No)
                            return;

                        try
                        {
                            File.Delete(result);
                        }
                        catch(Exception)
                        {
                            await MessageBoxManager.
                                  GetMessageBoxStandardWindow(Localization.DatabaseFileCannotDeleteTitle,
                                                              Localization.DatabaseFileCannotDeleteCaption,
                                                              ButtonEnum.Ok, Icon.Error).ShowDialog(_view);

                            return;
                        }
                    }
                }
                else
                {
                    btnResult = await MessageBoxManager.
                                      GetMessageBoxStandardWindow(Localization.DatabaseFileExistsMsgBoxTitle,
                                                                  Localization.DatabaseFileDeleteCaption,
                                                                  ButtonEnum.YesNo, Icon.Error).ShowDialog(_view);

                    if(btnResult == ButtonResult.No)
                        return;

                    try
                    {
                        File.Delete(result);
                    }
                    catch(Exception)
                    {
                        await MessageBoxManager.
                              GetMessageBoxStandardWindow(Localization.DatabaseFileCannotDeleteTitle,
                                                          Localization.DatabaseFileCannotDeleteCaption, ButtonEnum.Ok,
                                                          Icon.Error).ShowDialog(_view);

                        return;
                    }
                }
            }

            try
            {
                var ctx = Context.Create(result);
                await ctx.Database.MigrateAsync();
            }
            catch(Exception)
            {
                await MessageBoxManager.
                      GetMessageBoxStandardWindow(Localization.DatabaseFileUnusableMsgBoxTitle,
                                                  Localization.DatabaseFileUnusableMsgBoxCaption, ButtonEnum.Ok,
                                                  Icon.Error).ShowDialog(_view);

                return;
            }

            DatabasePath = result;
        }

        void ExecuteSaveCommand()
        {
            if(_databaseChanged)
                Settings.Settings.Current.DatabasePath = DatabasePath;

            if(_repositoryChanged)
                Settings.Settings.Current.RepositoryPath = RepositoryPath;

            if(_temporaryChanged)
                Settings.Settings.Current.TemporaryFolder = TemporaryPath;

            if(_unArChanged)
            {
                Settings.Settings.Current.UnArchiverPath = UnArPath;
                Settings.Settings.UnArUsable             = true;
            }

            if(_databaseChanged   ||
               _repositoryChanged ||
               _temporaryChanged  ||
               _unArChanged)
                Settings.Settings.SaveSettings();

            _view.Close();
        }
    }
}