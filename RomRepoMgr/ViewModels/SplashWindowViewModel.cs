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
// Copyright © 2020-2024 Natalia Portillo
*******************************************************************************/

using System;
using System.IO;
using System.Linq;
using System.Reactive;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Threading;
using Microsoft.EntityFrameworkCore;
using ReactiveUI;
using RomRepoMgr.Core.EventArgs;
using RomRepoMgr.Core.Models;
using RomRepoMgr.Core.Workers;
using RomRepoMgr.Database;

namespace RomRepoMgr.ViewModels;

public sealed class SplashWindowViewModel : ViewModelBase
{
    bool _checkingUnArError;
    bool _checkingUnArOk;
    bool _checkingUnArUnknown;
    bool _exitVisible;
    bool _loadingDatabaseError;
    bool _loadingDatabaseOk;
    bool _loadingDatabaseUnknown;
    bool _loadingRomSetsError;
    bool _loadingRomSetsOk;
    bool _loadingRomSetsUnknown;
    bool _loadingSettingsError;
    bool _loadingSettingsOk;
    bool _loadingSettingsUnknown;
    bool _migratingDatabaseError;
    bool _migratingDatabaseOk;
    bool _migratingDatabaseUnknown;

    public SplashWindowViewModel()
    {
        ExitCommand = ReactiveCommand.Create(ExecuteExitCommand);

        LoadingSettingsOk        = false;
        LoadingSettingsError     = false;
        LoadingSettingsUnknown   = true;
        CheckingUnArOk           = false;
        CheckingUnArError        = false;
        CheckingUnArUnknown      = true;
        LoadingDatabaseOk        = false;
        LoadingDatabaseError     = false;
        LoadingDatabaseUnknown   = true;
        MigratingDatabaseOk      = false;
        MigratingDatabaseError   = false;
        MigratingDatabaseUnknown = true;
        LoadingRomSetsOk         = false;
        LoadingRomSetsError      = false;
        LoadingRomSetsUnknown    = true;
        ExitVisible              = false;
    }

    public ReactiveCommand<Unit, Unit> ExitCommand { get; }

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

    public bool CheckingUnArOk
    {
        get => _checkingUnArOk;
        set => this.RaiseAndSetIfChanged(ref _checkingUnArOk, value);
    }

    public bool CheckingUnArError
    {
        get => _checkingUnArError;
        set => this.RaiseAndSetIfChanged(ref _checkingUnArError, value);
    }

    public bool CheckingUnArUnknown
    {
        get => _checkingUnArUnknown;
        set => this.RaiseAndSetIfChanged(ref _checkingUnArUnknown, value);
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

    public bool LoadingRomSetsOk
    {
        get => _loadingRomSetsOk;
        set => this.RaiseAndSetIfChanged(ref _loadingRomSetsOk, value);
    }

    public bool LoadingRomSetsError
    {
        get => _loadingRomSetsError;
        set => this.RaiseAndSetIfChanged(ref _loadingRomSetsError, value);
    }

    public bool LoadingRomSetsUnknown
    {
        get => _loadingRomSetsUnknown;
        set => this.RaiseAndSetIfChanged(ref _loadingRomSetsUnknown, value);
    }

    public string LoadingText => "ROM Repository Manager";

    void ExecuteExitCommand() =>
        (Application.Current.ApplicationLifetime as ClassicDesktopStyleApplicationLifetime)?.Shutdown();

    internal void OnOpened() => Dispatcher.UIThread.Post(LoadSettings);

    void LoadSettings()
    {
        _ = Task.Run(() =>
        {
            try
            {
                Settings.Settings.LoadSettings();

                Dispatcher.UIThread.Post(CheckUnAr);
            }
            catch(Exception e)
            {
                // TODO: Log error
                Dispatcher.UIThread.Post(FailedLoadingSettings);
            }
        });
    }

    void FailedLoadingSettings()
    {
        LoadingSettingsUnknown = false;
        LoadingSettingsError   = true;
        ExitVisible            = true;
    }

    void CheckUnAr()
    {
        _ = Task.Run(() =>
        {
            LoadingSettingsUnknown = false;
            LoadingSettingsOk      = true;

            try
            {
                var worker = new Compression();
                Settings.Settings.UnArUsable = worker.CheckUnAr(Settings.Settings.Current.UnArchiverPath);

                Dispatcher.UIThread.Post(LoadDatabase);
            }
            catch(Exception e)
            {
                // TODO: Log error
                Dispatcher.UIThread.Post(FailedCheckUnAr);
            }
        });
    }

    void FailedCheckUnAr()
    {
        CheckingUnArUnknown = false;
        CheckingUnArError   = true;
        ExitVisible         = true;
    }

    void LoadDatabase()
    {
        CheckingUnArUnknown = false;
        CheckingUnArOk      = true;

        _ = Task.Run(() =>
        {
            try
            {
                string dbPathFolder = Path.GetDirectoryName(Settings.Settings.Current.DatabasePath);

                if(!Directory.Exists(dbPathFolder)) Directory.CreateDirectory(dbPathFolder);

                using var ctx = Context.Create(Settings.Settings.Current.DatabasePath);

                Dispatcher.UIThread.Post(MigrateDatabase);
            }
            catch(Exception e)
            {
                // TODO: Log error
                Dispatcher.UIThread.Post(FailedLoadingDatabase);
            }
        });
    }

    void FailedLoadingDatabase()
    {
        LoadingDatabaseUnknown = false;
        LoadingDatabaseError   = true;
        ExitVisible            = true;
    }

    void MigrateDatabase()
    {
        LoadingDatabaseUnknown = false;
        LoadingDatabaseOk      = true;

        _ = Task.Run(() =>
        {
            try
            {
                using var ctx = Context.Create(Settings.Settings.Current.DatabasePath);

                ctx.Database.Migrate();

                Dispatcher.UIThread.Post(LoadRomSets);
            }
            catch(Exception e)
            {
                // TODO: Log error
                Dispatcher.UIThread.Post(FailedMigratingDatabase);
            }
        });
    }

    void FailedMigratingDatabase()
    {
        MigratingDatabaseUnknown = false;
        MigratingDatabaseError   = true;
        ExitVisible              = true;
    }

    void LoadRomSets()
    {
        MigratingDatabaseUnknown = false;
        MigratingDatabaseOk      = true;

        _ = Task.Run(() =>
        {
            try
            {
                using var ctx = Context.Create(Settings.Settings.Current.DatabasePath);

                GotRomSets?.Invoke(this,
                                   new RomSetsEventArgs
                                   {
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
                                                     })
                                                    .ToList()
                                   });

                Dispatcher.UIThread.Post(LoadMainWindow);
            }
            catch(Exception e)
            {
                // TODO: Log error
                Dispatcher.UIThread.Post(FailedLoadingRomSets);
            }
        });
    }

    void FailedLoadingRomSets()
    {
        LoadingRomSetsUnknown = false;
        LoadingRomSetsError   = true;
        ExitVisible           = true;
    }

    void LoadMainWindow()
    {
        LoadingRomSetsUnknown = false;
        LoadingRomSetsOk      = true;

        WorkFinished?.Invoke(this, EventArgs.Empty);
    }

    internal event EventHandler WorkFinished;

    internal event EventHandler<RomSetsEventArgs> GotRomSets;
}