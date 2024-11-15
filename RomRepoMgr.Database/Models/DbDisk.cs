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

using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;

namespace RomRepoMgr.Database.Models;

public class DbDisk : BaseModel<ulong>
{
    public ulong? Size { get; set; }
    [StringLength(32, MinimumLength = 32)]
    public string Md5 { get; set; }
    [StringLength(40, MinimumLength = 40)]
    public string Sha1 { get; set; }
    [DefaultValue(false)]
    public bool IsInRepo { get;                                       set; }
    public         string                     OriginalFileName { get; set; }
    public virtual ICollection<DiskByMachine> Machines         { get; set; }
}