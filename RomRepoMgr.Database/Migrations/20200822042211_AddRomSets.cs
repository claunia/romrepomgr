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

using System;
using Microsoft.EntityFrameworkCore.Migrations;

namespace RomRepoMgr.Database.Migrations
{
    public partial class AddRomSets : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable("RomSets", table => new
            {
                Id          = table.Column<long>(nullable: false).Annotation("Sqlite:Autoincrement", true),
                CreatedOn   = table.Column<DateTime>(nullable: false),
                UpdatedOn   = table.Column<DateTime>(nullable: false),
                Author      = table.Column<string>(nullable: true),
                Comment     = table.Column<string>(nullable: true),
                Date        = table.Column<string>(nullable: true),
                Description = table.Column<string>(nullable: true),
                Homepage    = table.Column<string>(nullable: true),
                Name        = table.Column<string>(nullable: true),
                Version     = table.Column<string>(nullable: true),
                Filename    = table.Column<string>(nullable: false),
                Sha384      = table.Column<string>(maxLength: 96, nullable: false)
            }, constraints: table =>
            {
                table.PrimaryKey("PK_RomSets", x => x.Id);
            });

            migrationBuilder.CreateIndex("IX_RomSets_Author", "RomSets", "Author");

            migrationBuilder.CreateIndex("IX_RomSets_Comment", "RomSets", "Comment");

            migrationBuilder.CreateIndex("IX_RomSets_Date", "RomSets", "Date");

            migrationBuilder.CreateIndex("IX_RomSets_Description", "RomSets", "Description");

            migrationBuilder.CreateIndex("IX_RomSets_Filename", "RomSets", "Filename");

            migrationBuilder.CreateIndex("IX_RomSets_Homepage", "RomSets", "Homepage");

            migrationBuilder.CreateIndex("IX_RomSets_Name", "RomSets", "Name");

            migrationBuilder.CreateIndex("IX_RomSets_Sha384", "RomSets", "Sha384");

            migrationBuilder.CreateIndex("IX_RomSets_Version", "RomSets", "Version");
        }

        protected override void Down(MigrationBuilder migrationBuilder) => migrationBuilder.DropTable("RomSets");
    }
}