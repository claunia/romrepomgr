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
using ReactiveUI;
using RomRepoMgr.Core.EventArgs;
using RomRepoMgr.Core.Models;
using RomRepoMgr.Database;
using RomRepoMgr.Database.Models;
using RomRepoMgr.Views;

namespace RomRepoMgr.ViewModels
{
    public class EditDatViewModel : ViewModelBase
    {
        readonly EditDat _view;
        string           _author;
        string           _comment;
        string           _date;
        string           _description;
        string           _homepage;

        bool                 _modified;
        string               _name;
        readonly RomSetModel _romSet;
        string               _version;

        public EditDatViewModel(EditDat view, RomSetModel romSet)
        {
            _view         = view;
            _romSet       = romSet;
            _name         = romSet.Name;
            _version      = romSet.Version;
            _author       = romSet.Author;
            _comment      = romSet.Comment;
            _date         = romSet.Date;
            _description  = romSet.Description;
            _homepage     = romSet.Homepage;
            SaveCommand   = ReactiveCommand.Create(ExecuteSaveCommand);
            CancelCommand = ReactiveCommand.Create(ExecuteCloseCommand);
            CloseCommand  = ReactiveCommand.Create(ExecuteCloseCommand);
        }

        public string NameLabel               => "Name";
        public string VersionLabel            => "Version";
        public string AuthorLabel             => "Author";
        public string CommentLabel            => "Comment";
        public string DateLabel               => "Date";
        public string DescriptionLabel        => "Description";
        public string HomepageLabel           => "Homepage";
        public string TotalMachinesLabel      => "Total games";
        public string CompleteMachinesLabel   => "Complete games";
        public string IncompleteMachinesLabel => "Incomplete games";
        public string TotalRomsLabel          => "Total ROMs";
        public string HaveRomsLabel           => "Have ROMs";
        public string MissRomsLabel           => "Missing ROMs";
        public string Title                   => "Edit DAT";
        public string SaveLabel               => "Save";
        public string CancelLabel             => "Cancel";
        public string CloseLabel              => "Close";

        public ReactiveCommand<Unit, Unit> SaveCommand        { get; }
        public ReactiveCommand<Unit, Unit> CancelCommand      { get; }
        public ReactiveCommand<Unit, Unit> CloseCommand       { get; }
        public long                        TotalMachines      => _romSet.TotalMachines;
        public long                        CompleteMachines   => _romSet.CompleteMachines;
        public long                        IncompleteMachines => _romSet.IncompleteMachines;
        public long                        TotalRoms          => _romSet.TotalRoms;
        public long                        HaveRoms           => _romSet.HaveRoms;
        public long                        MissRoms           => _romSet.MissRoms;

        public bool Modified
        {
            get => _modified;
            set => this.RaiseAndSetIfChanged(ref _modified, value);
        }

        public string Name
        {
            get => _name;
            set
            {
                if(value != _name)
                    Modified = true;

                this.RaiseAndSetIfChanged(ref _name, value);
            }
        }

        public string Version
        {
            get => _version;
            set
            {
                if(value != _version)
                    Modified = true;

                this.RaiseAndSetIfChanged(ref _version, value);
            }
        }

        public string Author
        {
            get => _author;
            set
            {
                if(value != _author)
                    Modified = true;

                this.RaiseAndSetIfChanged(ref _author, value);
            }
        }

        public string Comment
        {
            get => _comment;
            set
            {
                if(value != _comment)
                    Modified = true;

                this.RaiseAndSetIfChanged(ref _comment, value);
            }
        }

        public string Date
        {
            get => _date;
            set
            {
                if(value != _date)
                    Modified = true;

                this.RaiseAndSetIfChanged(ref _date, value);
            }
        }

        public string Description
        {
            get => _description;
            set
            {
                if(value != _description)
                    Modified = true;

                this.RaiseAndSetIfChanged(ref _description, value);
            }
        }

        public string Homepage
        {
            get => _homepage;
            set
            {
                if(value != _homepage)
                    Modified = true;

                this.RaiseAndSetIfChanged(ref _homepage, value);
            }
        }

        public EventHandler<RomSetEventArgs> RomSetModified { get; set; }

        void ExecuteCloseCommand() => _view.Close();

        async void ExecuteSaveCommand()
        {
            RomSet romSetDb = await Context.Singleton.RomSets.FindAsync(_romSet.Id);

            if(romSetDb == null)
                return;

            romSetDb.Author      = Author;
            romSetDb.Comment     = Comment;
            romSetDb.Date        = Date;
            romSetDb.Description = Description;
            romSetDb.Homepage    = Homepage;
            romSetDb.Name        = Name;
            romSetDb.Version     = Version;
            romSetDb.UpdatedOn   = DateTime.UtcNow;

            await Context.Singleton.SaveChangesAsync();

            RomSetModified?.Invoke(this, new RomSetEventArgs
            {
                RomSet = new RomSetModel
                {
                    Author             = Author,
                    Comment            = Comment,
                    Date               = Date,
                    Description        = Description,
                    Homepage           = Homepage,
                    Name               = Name,
                    Version            = Version,
                    Filename           = _romSet.Filename,
                    Sha384             = _romSet.Sha384,
                    TotalMachines      = _romSet.TotalMachines,
                    CompleteMachines   = _romSet.CompleteMachines,
                    IncompleteMachines = _romSet.IncompleteMachines,
                    TotalRoms          = _romSet.TotalRoms,
                    HaveRoms           = _romSet.HaveRoms,
                    MissRoms           = _romSet.MissRoms,
                    Id                 = _romSet.Id
                }
            });

            Modified = false;
        }
    }
}