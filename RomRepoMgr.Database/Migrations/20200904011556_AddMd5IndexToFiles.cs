using Microsoft.EntityFrameworkCore.Migrations;

namespace RomRepoMgr.Database.Migrations
{
    public partial class AddMd5IndexToFiles : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder) =>
            migrationBuilder.CreateIndex("IX_Files_Md5", "Files", "Md5");

        protected override void Down(MigrationBuilder migrationBuilder) =>
            migrationBuilder.DropIndex("IX_Files_Md5", "Files");
    }
}