using Microsoft.EntityFrameworkCore.Migrations;

namespace RomRepoMgr.Database.Migrations
{
    public partial class AddMissingIndexes : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex("IX_Files_IsInRepo", "Files", "IsInRepo");

            migrationBuilder.CreateIndex("IX_Disks_IsInRepo", "Disks", "IsInRepo");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex("IX_Files_IsInRepo", "Files");

            migrationBuilder.DropIndex("IX_Disks_IsInRepo", "Disks");
        }
    }
}