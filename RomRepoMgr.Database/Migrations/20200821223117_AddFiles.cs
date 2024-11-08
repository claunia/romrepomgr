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
using Microsoft.EntityFrameworkCore.Migrations;

namespace RomRepoMgr.Database.Migrations
{
    public partial class AddFiles : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable("Files", table => new
            {
                Id        = table.Column<ulong>(nullable: false).Annotation("Sqlite:Autoincrement", true),
                CreatedOn = table.Column<DateTime>(nullable: false),
                UpdatedOn = table.Column<DateTime>(nullable: false),
                Size      = table.Column<ulong>(nullable: false),
                Crc32     = table.Column<string>(maxLength: 8, nullable: true),
                Md5       = table.Column<string>(maxLength: 32, nullable: true),
                Sha1      = table.Column<string>(maxLength: 40, nullable: true),
                Sha256    = table.Column<string>(maxLength: 64, nullable: true)
            }, constraints: table =>
            {
                table.PrimaryKey("PK_Files", x => x.Id);
            });

            migrationBuilder.CreateIndex("IX_Files_Crc32", "Files", "Crc32");

            migrationBuilder.CreateIndex("IX_Files_Sha1", "Files", "Sha1");

            migrationBuilder.CreateIndex("IX_Files_Sha256", "Files", "Sha256");

            migrationBuilder.CreateIndex("IX_Files_Size", "Files", "Size");
        }

        protected override void Down(MigrationBuilder migrationBuilder) => migrationBuilder.DropTable("Files");
    }
}