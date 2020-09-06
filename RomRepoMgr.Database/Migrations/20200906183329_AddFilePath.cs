using Microsoft.EntityFrameworkCore.Migrations;

namespace RomRepoMgr.Database.Migrations
{
    public partial class AddFilePath : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder) =>
            migrationBuilder.AddColumn<string>("Path", "FilesByMachines", maxLength: 4096, nullable: true);

        protected override void Down(MigrationBuilder migrationBuilder) =>
            migrationBuilder.DropColumn("Path", "FilesByMachines");
    }
}