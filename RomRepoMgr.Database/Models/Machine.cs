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
// Copyright © 2020-2026 Natalia Portillo
*******************************************************************************/

using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace RomRepoMgr.Database.Models;

public class Machine : BaseModel<ulong>
{
    [Required]
    public string Name { get; set; }
    [Required]
    public virtual RomSet RomSet { get;                        set; }
    public virtual ICollection<FileByMachine>  Files    { get; set; }
    public virtual ICollection<DiskByMachine>  Disks    { get; set; }
    public virtual ICollection<MediaByMachine> Medias   { get; set; }
    public         long                        RomSetId { get; set; }
}