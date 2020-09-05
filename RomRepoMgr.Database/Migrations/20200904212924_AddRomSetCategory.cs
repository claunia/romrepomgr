using Microsoft.EntityFrameworkCore.Migrations;

namespace RomRepoMgr.Database.Migrations
{
    public partial class AddRomSetCategory : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>("Category", "RomSets", nullable: true);

            migrationBuilder.CreateIndex("IX_RomSets_Category", "RomSets", "Category");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex("IX_RomSets_Category", "RomSets");

            migrationBuilder.DropColumn("Category", "RomSets");
        }
    }
}