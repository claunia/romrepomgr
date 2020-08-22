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
using Microsoft.EntityFrameworkCore.Migrations;

namespace RomRepoMgr.Database.Migrations
{
    public partial class AddMachine : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable("Machines", table => new
            {
                Id        = table.Column<ulong>(nullable: false).Annotation("Sqlite:Autoincrement", true),
                CreatedOn = table.Column<DateTime>(nullable: false),
                UpdatedOn = table.Column<DateTime>(nullable: false),
                Name      = table.Column<string>(nullable: false),
                RomSetId  = table.Column<long>(nullable: false)
            }, constraints: table =>
            {
                table.PrimaryKey("PK_Machines", x => x.Id);

                table.ForeignKey("FK_Machines_RomSets_RomSetId", x => x.RomSetId, "RomSets", "Id",
                                 onDelete: ReferentialAction.Cascade);
            });

            migrationBuilder.CreateIndex("IX_Machines_Name", "Machines", "Name");

            migrationBuilder.CreateIndex("IX_Machines_RomSetId", "Machines", "RomSetId");
        }

        protected override void Down(MigrationBuilder migrationBuilder) => migrationBuilder.DropTable("Machines");
    }
}