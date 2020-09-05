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

namespace RomRepoMgr.Core.Models
{
    public class RomSetModel
    {
        public string Author             { get; set; }
        public string Comment            { get; set; }
        public string Date               { get; set; }
        public string Description        { get; set; }
        public string Homepage           { get; set; }
        public string Name               { get; set; }
        public string Version            { get; set; }
        public string Filename           { get; set; }
        public string Sha384             { get; set; }
        public long   TotalMachines      { get; set; }
        public long   CompleteMachines   { get; set; }
        public long   IncompleteMachines { get; set; }
        public long   TotalRoms          { get; set; }
        public long   HaveRoms           { get; set; }
        public long   MissRoms           { get; set; }
        public long   Id                 { get; set; }
        public string Category           { get; set; }
    }
}