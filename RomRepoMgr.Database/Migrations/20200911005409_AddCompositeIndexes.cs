using Microsoft.EntityFrameworkCore.Migrations;

namespace RomRepoMgr.Database.Migrations
{
    public partial class AddCompositeIndexes : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex("IX_Files_Crc32_Size", "Files", new[]
            {
                "Crc32", "Size"
            });

            migrationBuilder.CreateIndex("IX_Files_Md5_Size", "Files", new[]
            {
                "Md5", "Size"
            });

            migrationBuilder.CreateIndex("IX_Files_Sha1_Size", "Files", new[]
            {
                "Sha1", "Size"
            });

            migrationBuilder.CreateIndex("IX_Files_Sha256_Size", "Files", new[]
            {
                "Sha256", "Size"
            });

            migrationBuilder.CreateIndex("IX_Files_Sha384_Size", "Files", new[]
            {
                "Sha384", "Size"
            });

            migrationBuilder.CreateIndex("IX_Files_Sha512_Size", "Files", new[]
            {
                "Sha512", "Size"
            });
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex("IX_Files_Crc32_Size", "Files");

            migrationBuilder.DropIndex("IX_Files_Md5_Size", "Files");

            migrationBuilder.DropIndex("IX_Files_Sha1_Size", "Files");

            migrationBuilder.DropIndex("IX_Files_Sha256_Size", "Files");

            migrationBuilder.DropIndex("IX_Files_Sha384_Size", "Files");

            migrationBuilder.DropIndex("IX_Files_Sha512_Size", "Files");
        }
    }
}