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
// Copyright © 2020 Natalia Portillo
*******************************************************************************/

using System;
using System.Reactive;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Threading;
using ReactiveUI;

namespace RomRepoMgr.ViewModels
{
    public sealed class SplashWindowViewModel : ViewModelBase
    {
        string _exitButtonText;
        bool   _exitVisible;
        bool   _loadingDatabaseError;
        bool   _loadingDatabaseOk;
        string _loadingDatabaseText;
        bool   _loadingDatabaseUnknown;
        bool   _loadingSettingsError;
        bool   _loadingSettingsOk;
        string _loadingSettingsText;
        bool   _loadingSettingsUnknown;
        string _loadingText;
        bool   _migratingDatabaseError;
        bool   _migratingDatabaseOk;
        string _migratingDatabaseText;
        bool   _migratingDatabaseUnknown;

        public SplashWindowViewModel()
        {
            ExitCommand = ReactiveCommand.Create(ExecuteExitCommand);

            LoadStrings();

            LoadingSettingsOk        = false;
            LoadingSettingsError     = false;
            LoadingSettingsUnknown   = true;
            LoadingDatabaseOk        = false;
            LoadingDatabaseError     = false;
            LoadingDatabaseUnknown   = true;
            MigratingDatabaseOk      = false;
            MigratingDatabaseError   = false;
            MigratingDatabaseUnknown = true;
            ExitVisible              = false;
        }

        public ReactiveCommand<Unit, Unit> ExitCommand { get; }

        public string LoadingText
        {
            get => _loadingText;
            set => this.RaiseAndSetIfChanged(ref _loadingText, value);
        }

        public string LoadingSettingsText
        {
            get => _loadingSettingsText;
            set => this.RaiseAndSetIfChanged(ref _loadingSettingsText, value);
        }

        public bool LoadingSettingsOk
        {
            get => _loadingSettingsOk;
            set => this.RaiseAndSetIfChanged(ref _loadingSettingsOk, value);
        }

        public bool LoadingSettingsError
        {
            get => _loadingSettingsError;
            set => this.RaiseAndSetIfChanged(ref _loadingSettingsError, value);
        }

        public bool LoadingSettingsUnknown
        {
            get => _loadingSettingsUnknown;
            set => this.RaiseAndSetIfChanged(ref _loadingSettingsUnknown, value);
        }

        public string LoadingDatabaseText
        {
            get => _loadingDatabaseText;
            set => this.RaiseAndSetIfChanged(ref _loadingDatabaseText, value);
        }

        public bool LoadingDatabaseOk
        {
            get => _loadingDatabaseOk;
            set => this.RaiseAndSetIfChanged(ref _loadingDatabaseOk, value);
        }

        public bool LoadingDatabaseError
        {
            get => _loadingDatabaseError;
            set => this.RaiseAndSetIfChanged(ref _loadingDatabaseError, value);
        }

        public bool LoadingDatabaseUnknown
        {
            get => _loadingDatabaseUnknown;
            set => this.RaiseAndSetIfChanged(ref _loadingDatabaseUnknown, value);
        }

        public string MigratingDatabaseText
        {
            get => _migratingDatabaseText;
            set => this.RaiseAndSetIfChanged(ref _migratingDatabaseText, value);
        }

        public bool MigratingDatabaseOk
        {
            get => _migratingDatabaseOk;
            set => this.RaiseAndSetIfChanged(ref _migratingDatabaseOk, value);
        }

        public bool MigratingDatabaseError
        {
            get => _migratingDatabaseError;
            set => this.RaiseAndSetIfChanged(ref _migratingDatabaseError, value);
        }

        public bool MigratingDatabaseUnknown
        {
            get => _migratingDatabaseUnknown;
            set => this.RaiseAndSetIfChanged(ref _migratingDatabaseUnknown, value);
        }

        public bool ExitVisible
        {
            get => _exitVisible;
            set => this.RaiseAndSetIfChanged(ref _exitVisible, value);
        }

        public string ExitButtonText
        {
            get => _exitButtonText;
            set => this.RaiseAndSetIfChanged(ref _exitButtonText, value);
        }

        internal void ExecuteExitCommand() =>
            (Application.Current.ApplicationLifetime as ClassicDesktopStyleApplicationLifetime)?.Shutdown();

        void LoadStrings()
        {
            LoadingText           = "ROM Repository Manager";
            LoadingSettingsText   = "Loading settings...";
            LoadingDatabaseText   = "Loading database...";
            MigratingDatabaseText = "Migrating database...";
            ExitButtonText        = "Exit";
        }

        internal void OnOpened() => Dispatcher.UIThread.Post(LoadSettings);

        void LoadSettings() => Task.Run(() =>
        {
            try
            {
                Settings.Settings.LoadSettings();
            }
            catch(Exception e)
            {
                // TODO: Log error
                Dispatcher.UIThread.Post(FailedLoadingSettings);
            }

            Dispatcher.UIThread.Post(LoadDatabase);
        });

        void FailedLoadingSettings()
        {
            LoadingSettingsUnknown = false;
            LoadingSettingsError   = true;
            ExitVisible            = true;
        }

        void LoadDatabase()
        {
            LoadingSettingsUnknown = false;
            LoadingSettingsOk      = true;
        }
    }
}