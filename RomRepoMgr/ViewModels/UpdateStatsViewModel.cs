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
using System.Collections.ObjectModel;
using System.Linq;
using System.Reactive;
using System.Threading.Tasks;
using Avalonia.Threading;
using Microsoft.EntityFrameworkCore;
using ReactiveUI;
using RomRepoMgr.Core.Models;
using RomRepoMgr.Database;
using RomRepoMgr.Database.Models;
using RomRepoMgr.Resources;
using RomRepoMgr.Views;

namespace RomRepoMgr.ViewModels;

public sealed class UpdateStatsViewModel : ViewModelBase
{
    readonly UpdateStats _view;
    bool                 _canClose;
    double               _currentValue;
    bool                 _indeterminateProgress;
    double               _maximumValue;
    double               _minimumValue;
    bool                 _progressVisible;
    RomSetModel          _selectedRomSet;
    string               _statusMessage;

    // Mock
    public UpdateStatsViewModel() {}

    public UpdateStatsViewModel(UpdateStats view)
    {
        _view                 = view;
        CloseCommand          = ReactiveCommand.Create(ExecuteCloseCommand);
        IndeterminateProgress = true;
        ProgressVisible       = false;
        RomSets               = [];
    }

    public string StatusMessage
    {
        get => _statusMessage;
        set => this.RaiseAndSetIfChanged(ref _statusMessage, value);
    }

    public bool IndeterminateProgress
    {
        get => _indeterminateProgress;
        set => this.RaiseAndSetIfChanged(ref _indeterminateProgress, value);
    }

    public double MaximumValue
    {
        get => _maximumValue;
        set => this.RaiseAndSetIfChanged(ref _maximumValue, value);
    }

    public double MinimumValue
    {
        get => _minimumValue;
        set => this.RaiseAndSetIfChanged(ref _minimumValue, value);
    }

    public double CurrentValue
    {
        get => _currentValue;
        set => this.RaiseAndSetIfChanged(ref _currentValue, value);
    }

    public bool ProgressVisible
    {
        get => _progressVisible;
        set => this.RaiseAndSetIfChanged(ref _progressVisible, value);
    }

    public RomSetModel SelectedRomSet
    {
        get => _selectedRomSet;
        set => this.RaiseAndSetIfChanged(ref _selectedRomSet, value);
    }

    public bool CanClose
    {
        get => _canClose;
        set => this.RaiseAndSetIfChanged(ref _canClose, value);
    }

    public ObservableCollection<RomSetModel> RomSets { get; }

    public ReactiveCommand<Unit, Unit> CloseCommand { get; }

    internal void OnOpened()
    {
        _ = Task.Run(() =>
        {
            using var ctx = Context.Create(Settings.Settings.Current.DatabasePath);

            Dispatcher.UIThread.Post(() =>
            {
                StatusMessage         = Localization.RetrievingRomSetsFromDatabase;
                ProgressVisible       = true;
                IndeterminateProgress = true;
            });

            long romSetCount = ctx.RomSets.LongCount();

            Dispatcher.UIThread.Post(() => StatusMessage = Localization.RemovingOldStatistics);

            ctx.Database.ExecuteSql($"DELETE FROM \"RomSetStats\"");

            Dispatcher.UIThread.Post(() =>
            {
                IndeterminateProgress = false;
                MinimumValue          = 0;
                MaximumValue          = romSetCount;
                CurrentValue          = 0;
            });

            long pos = 0;

            foreach(RomSet romSet in ctx.RomSets)
            {
                long currentPos = pos;

                Dispatcher.UIThread.Post(() =>
                {
                    StatusMessage = string.Format(Localization.CalculatingStatisticsForRomSet,
                                                  romSet.Name,
                                                  romSet.Version,
                                                  romSet.Description);

                    CurrentValue = currentPos;
                });

                try
                {
                    RomSetStat stats = ctx.RomSets.Where(r => r.Id == romSet.Id)
                                          .Select(r => new RomSetStat
                                           {
                                               RomSetId      = r.Id,
                                               TotalMachines = r.Machines.Count,
                                               CompleteMachines =
                                                   r.Machines.Count(m => m.Files.Count > 0  &&
                                                                         m.Disks.Count == 0 &&
                                                                         m.Files.All(f => f.File.IsInRepo)) +
                                                   r.Machines.Count(m => m.Disks.Count > 0  &&
                                                                         m.Files.Count == 0 &&
                                                                         m.Disks.All(f => f.Disk.IsInRepo)) +
                                                   r.Machines.Count(m => m.Files.Count > 0                 &&
                                                                         m.Disks.Count > 0                 &&
                                                                         m.Files.All(f => f.File.IsInRepo) &&
                                                                         m.Disks.All(f => f.Disk.IsInRepo)),
                                               IncompleteMachines =
                                                   r.Machines.Count(m => m.Files.Count > 0  &&
                                                                         m.Disks.Count == 0 &&
                                                                         m.Files.Any(f => !f.File.IsInRepo)) +
                                                   r.Machines.Count(m => m.Disks.Count > 0  &&
                                                                         m.Files.Count == 0 &&
                                                                         m.Disks.Any(f => !f.Disk.IsInRepo)) +
                                                   r.Machines.Count(m => m.Files.Count > 0 &&
                                                                         m.Disks.Count > 0 &&
                                                                         (m.Files.Any(f => !f.File.IsInRepo) ||
                                                                          m.Disks.Any(f => !f.Disk.IsInRepo))),
                                               TotalRoms =
                                                   r.Machines.Sum(m => m.Files.Count) +
                                                   r.Machines.Sum(m => m.Disks.Count) +
                                                   r.Machines.Sum(m => m.Medias.Count),
                                               HaveRoms = r.Machines.Sum(m => m.Files.Count(f => f.File.IsInRepo)) +
                                                          r.Machines.Sum(m => m.Disks.Count(f => f.Disk.IsInRepo)) +
                                                          r.Machines.Sum(m => m.Medias.Count(f => f.Media.IsInRepo)),
                                               MissRoms = r.Machines.Sum(m => m.Files.Count(f => !f.File.IsInRepo)) +
                                                          r.Machines.Sum(m => m.Disks.Count(f => !f.Disk.IsInRepo)) +
                                                          r.Machines.Sum(m => m.Medias.Count(f => !f.Media.IsInRepo))
                                           })
                                          .FirstOrDefault();

                    ctx.RomSetStats.Add(stats);

                    Dispatcher.UIThread.Post(() =>
                    {
                        RomSets.Add(new RomSetModel
                        {
                            Id                 = romSet.Id,
                            Author             = romSet.Author,
                            Comment            = romSet.Comment,
                            Date               = romSet.Date,
                            Description        = romSet.Description,
                            Filename           = romSet.Filename,
                            Homepage           = romSet.Homepage,
                            Name               = romSet.Name,
                            Sha384             = romSet.Sha384,
                            Version            = romSet.Version,
                            TotalMachines      = stats.TotalMachines,
                            CompleteMachines   = stats.CompleteMachines,
                            IncompleteMachines = stats.IncompleteMachines,
                            TotalRoms          = stats.TotalRoms,
                            HaveRoms           = stats.HaveRoms,
                            MissRoms           = stats.MissRoms,
                            Category           = romSet.Category
                        });
                    });
                }
                catch(Exception)
                {
                    // Ignored
                }

                pos++;
            }

            Dispatcher.UIThread.Post(() =>
            {
                StatusMessage         = Localization.SavingChangesToDatabase;
                ProgressVisible       = true;
                IndeterminateProgress = true;
            });

            ctx.SaveChanges();

            Dispatcher.UIThread.Post(() =>
            {
                StatusMessage   = Localization.Finished;
                ProgressVisible = false;
                CanClose        = true;
            });
        });
    }

    void ExecuteCloseCommand() => _view.Close();
}