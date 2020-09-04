using System;
using Microsoft.EntityFrameworkCore.Migrations;

namespace RomRepoMgr.Database.Migrations
{
    public partial class AddDisks : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable("Disks", table => new
            {
                Id               = table.Column<ulong>(nullable: false).Annotation("Sqlite:Autoincrement", true),
                CreatedOn        = table.Column<DateTime>(nullable: false),
                UpdatedOn        = table.Column<DateTime>(nullable: false),
                Size             = table.Column<ulong>(nullable: true),
                Md5              = table.Column<string>(maxLength: 32, nullable: true),
                Sha1             = table.Column<string>(maxLength: 40, nullable: true),
                IsInRepo         = table.Column<bool>(nullable: false),
                OriginalFileName = table.Column<string>(nullable: true)
            }, constraints: table => table.PrimaryKey("PK_Disks", x => x.Id));

            migrationBuilder.CreateTable("DisksByMachines", table => new
            {
                Id        = table.Column<ulong>(nullable: false).Annotation("Sqlite:Autoincrement", true),
                DiskId    = table.Column<ulong>(nullable: false),
                MachineId = table.Column<ulong>(nullable: false),
                Name      = table.Column<string>(nullable: false)
            }, constraints: table =>
            {
                table.PrimaryKey("PK_DisksByMachines", x => x.Id);

                table.ForeignKey("FK_DisksByMachines_Disks_DiskId", x => x.DiskId, "Disks", "Id",
                                 onDelete: ReferentialAction.Cascade);

                table.ForeignKey("FK_DisksByMachines_Machines_MachineId", x => x.MachineId, "Machines", "Id",
                                 onDelete: ReferentialAction.Cascade);
            });

            migrationBuilder.CreateIndex("IX_Disks_Md5", "Disks", "Md5");

            migrationBuilder.CreateIndex("IX_Disks_Sha1", "Disks", "Sha1");

            migrationBuilder.CreateIndex("IX_Disks_Size", "Disks", "Size");

            migrationBuilder.CreateIndex("IX_DisksByMachines_DiskId", "DisksByMachines", "DiskId");

            migrationBuilder.CreateIndex("IX_DisksByMachines_MachineId", "DisksByMachines", "MachineId");

            migrationBuilder.CreateIndex("IX_DisksByMachines_Name", "DisksByMachines", "Name");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable("DisksByMachines");

            migrationBuilder.DropTable("Disks");
        }
    }
}