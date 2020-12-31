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
// Copyright © 2020-2021 Natalia Portillo
*******************************************************************************/

using Microsoft.EntityFrameworkCore.Migrations;

namespace RomRepoMgr.Database.Migrations
{
    public partial class AddFilesByMachines : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable("FilesByMachines", table => new
            {
                Id        = table.Column<ulong>(nullable: false).Annotation("Sqlite:Autoincrement", true),
                FileId    = table.Column<ulong>(nullable: false),
                MachineId = table.Column<ulong>(nullable: false),
                Name      = table.Column<string>(nullable: false)
            }, constraints: table =>
            {
                table.PrimaryKey("PK_FilesByMachines", x => x.Id);

                table.ForeignKey("FK_FilesByMachines_Files_FileId", x => x.FileId, "Files", "Id",
                                 onDelete: ReferentialAction.Cascade);

                table.ForeignKey("FK_FilesByMachines_Machines_MachineId", x => x.MachineId, "Machines", "Id",
                                 onDelete: ReferentialAction.Cascade);
            });

            migrationBuilder.CreateIndex("IX_FilesByMachines_FileId", "FilesByMachines", "FileId");

            migrationBuilder.CreateIndex("IX_FilesByMachines_MachineId", "FilesByMachines", "MachineId");

            migrationBuilder.CreateIndex("IX_FilesByMachines_Name", "FilesByMachines", "Name");
        }

        protected override void Down(MigrationBuilder migrationBuilder) =>
            migrationBuilder.DropTable("FilesByMachines");
    }
}