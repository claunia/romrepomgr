using System;
using Microsoft.EntityFrameworkCore.Migrations;

namespace RomRepoMgr.Database.Migrations
{
    public partial class AddMedias : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable("Medias", table => new
            {
                Id               = table.Column<ulong>(nullable: false).Annotation("Sqlite:Autoincrement", true),
                CreatedOn        = table.Column<DateTime>(nullable: false),
                UpdatedOn        = table.Column<DateTime>(nullable: false),
                Size             = table.Column<ulong>(nullable: true),
                Md5              = table.Column<string>(maxLength: 32, nullable: true),
                Sha1             = table.Column<string>(maxLength: 40, nullable: true),
                Sha256           = table.Column<string>(maxLength: 64, nullable: true),
                SpamSum          = table.Column<string>(nullable: true),
                IsInRepo         = table.Column<bool>(nullable: false),
                OriginalFileName = table.Column<string>(nullable: true)
            }, constraints: table =>
            {
                table.PrimaryKey("PK_Medias", x => x.Id);
            });

            migrationBuilder.CreateTable("MediasByMachines", table => new
            {
                Id        = table.Column<ulong>(nullable: false).Annotation("Sqlite:Autoincrement", true),
                MediaId   = table.Column<ulong>(nullable: false),
                MachineId = table.Column<ulong>(nullable: false),
                Name      = table.Column<string>(nullable: false)
            }, constraints: table =>
            {
                table.PrimaryKey("PK_MediasByMachines", x => x.Id);

                table.ForeignKey("FK_MediasByMachines_Machines_MachineId", x => x.MachineId, "Machines", "Id",
                                 onDelete: ReferentialAction.Cascade);

                table.ForeignKey("FK_MediasByMachines_Medias_MediaId", x => x.MediaId, "Medias", "Id",
                                 onDelete: ReferentialAction.Cascade);
            });

            migrationBuilder.CreateIndex("IX_Medias_IsInRepo", "Medias", "IsInRepo");

            migrationBuilder.CreateIndex("IX_Medias_Md5", "Medias", "Md5");

            migrationBuilder.CreateIndex("IX_Medias_Sha1", "Medias", "Sha1");

            migrationBuilder.CreateIndex("IX_Medias_Sha256", "Medias", "Sha256");

            migrationBuilder.CreateIndex("IX_Medias_Size", "Medias", "Size");

            migrationBuilder.CreateIndex("IX_Medias_SpamSum", "Medias", "SpamSum");

            migrationBuilder.CreateIndex("IX_MediasByMachines_MachineId", "MediasByMachines", "MachineId");

            migrationBuilder.CreateIndex("IX_MediasByMachines_MediaId", "MediasByMachines", "MediaId");

            migrationBuilder.CreateIndex("IX_MediasByMachines_Name", "MediasByMachines", "Name");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable("MediasByMachines");

            migrationBuilder.DropTable("Medias");
        }
    }
}