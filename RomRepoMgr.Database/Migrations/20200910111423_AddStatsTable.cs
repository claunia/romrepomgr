﻿using Microsoft.EntityFrameworkCore.Migrations;

namespace RomRepoMgr.Database.Migrations
{
    public partial class AddStatsTable : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable("RomSetStats", table => new
            {
                RomSetId           = table.Column<long>(nullable: false),
                TotalMachines      = table.Column<long>(nullable: false),
                CompleteMachines   = table.Column<long>(nullable: false),
                IncompleteMachines = table.Column<long>(nullable: false),
                TotalRoms          = table.Column<long>(nullable: false),
                HaveRoms           = table.Column<long>(nullable: false),
                MissRoms           = table.Column<long>(nullable: false)
            }, constraints: table =>
            {
                table.PrimaryKey("PK_RomSetStats", x => x.RomSetId);

                table.ForeignKey("FK_RomSetStats_RomSets_RomSetId", x => x.RomSetId, "RomSets", "Id",
                                 onDelete: ReferentialAction.Cascade);
            });

            migrationBuilder.Sql(
                                 "INSERT INTO \"RomSetStats\" SELECT \"r\".\"Id\" AS \"RomSetId\", CAST((\n          SELECT COUNT(*)\n          FROM \"Machines\" AS \"m\"\n          WHERE \"r\".\"Id\" = \"m\".\"RomSetId\") AS INTEGER) AS \"TotalMachines\", CAST((((\n          SELECT COUNT(*)\n          FROM \"Machines\" AS \"m0\"\n          WHERE (\"r\".\"Id\" = \"m0\".\"RomSetId\") AND ((((\n              SELECT COUNT(*)\n              FROM \"FilesByMachines\" AS \"f\"\n              WHERE \"m0\".\"Id\" = \"f\".\"MachineId\") > 0) AND ((\n              SELECT COUNT(*)\n              FROM \"DisksByMachines\" AS \"d\"\n              WHERE \"m0\".\"Id\" = \"d\".\"MachineId\") = 0)) AND NOT EXISTS (\n              SELECT 1\n              FROM \"FilesByMachines\" AS \"f0\"\n              INNER JOIN \"Files\" AS \"f1\" ON \"f0\".\"FileId\" = \"f1\".\"Id\"\n              WHERE (\"m0\".\"Id\" = \"f0\".\"MachineId\") AND NOT (\"f1\".\"IsInRepo\")))) + (\n          SELECT COUNT(*)\n          FROM \"Machines\" AS \"m1\"\n          WHERE (\"r\".\"Id\" = \"m1\".\"RomSetId\") AND ((((\n              SELECT COUNT(*)\n              FROM \"DisksByMachines\" AS \"d0\"\n              WHERE \"m1\".\"Id\" = \"d0\".\"MachineId\") > 0) AND ((\n              SELECT COUNT(*)\n              FROM \"FilesByMachines\" AS \"f2\"\n              WHERE \"m1\".\"Id\" = \"f2\".\"MachineId\") = 0)) AND NOT EXISTS (\n              SELECT 1\n              FROM \"DisksByMachines\" AS \"d1\"\n              INNER JOIN \"Disks\" AS \"d2\" ON \"d1\".\"DiskId\" = \"d2\".\"Id\"\n              WHERE (\"m1\".\"Id\" = \"d1\".\"MachineId\") AND NOT (\"d2\".\"IsInRepo\"))))) + (\n          SELECT COUNT(*)\n          FROM \"Machines\" AS \"m2\"\n          WHERE (\"r\".\"Id\" = \"m2\".\"RomSetId\") AND (((((\n              SELECT COUNT(*)\n              FROM \"FilesByMachines\" AS \"f3\"\n              WHERE \"m2\".\"Id\" = \"f3\".\"MachineId\") > 0) AND ((\n              SELECT COUNT(*)\n              FROM \"DisksByMachines\" AS \"d3\"\n              WHERE \"m2\".\"Id\" = \"d3\".\"MachineId\") > 0)) AND NOT EXISTS (\n              SELECT 1\n              FROM \"FilesByMachines\" AS \"f4\"\n              INNER JOIN \"Files\" AS \"f5\" ON \"f4\".\"FileId\" = \"f5\".\"Id\"\n              WHERE (\"m2\".\"Id\" = \"f4\".\"MachineId\") AND NOT (\"f5\".\"IsInRepo\"))) AND NOT EXISTS (\n              SELECT 1\n              FROM \"DisksByMachines\" AS \"d4\"\n              INNER JOIN \"Disks\" AS \"d5\" ON \"d4\".\"DiskId\" = \"d5\".\"Id\"\n              WHERE (\"m2\".\"Id\" = \"d4\".\"MachineId\") AND NOT (\"d5\".\"IsInRepo\"))))) AS INTEGER) AS \"CompleteMachines\", CAST((((\n          SELECT COUNT(*)\n          FROM \"Machines\" AS \"m3\"\n          WHERE (\"r\".\"Id\" = \"m3\".\"RomSetId\") AND ((((\n              SELECT COUNT(*)\n              FROM \"FilesByMachines\" AS \"f6\"\n              WHERE \"m3\".\"Id\" = \"f6\".\"MachineId\") > 0) AND ((\n              SELECT COUNT(*)\n              FROM \"DisksByMachines\" AS \"d6\"\n              WHERE \"m3\".\"Id\" = \"d6\".\"MachineId\") = 0)) AND EXISTS (\n              SELECT 1\n              FROM \"FilesByMachines\" AS \"f7\"\n              INNER JOIN \"Files\" AS \"f8\" ON \"f7\".\"FileId\" = \"f8\".\"Id\"\n              WHERE (\"m3\".\"Id\" = \"f7\".\"MachineId\") AND NOT (\"f8\".\"IsInRepo\")))) + (\n          SELECT COUNT(*)\n          FROM \"Machines\" AS \"m4\"\n          WHERE (\"r\".\"Id\" = \"m4\".\"RomSetId\") AND ((((\n              SELECT COUNT(*)\n              FROM \"DisksByMachines\" AS \"d7\"\n              WHERE \"m4\".\"Id\" = \"d7\".\"MachineId\") > 0) AND ((\n              SELECT COUNT(*)\n              FROM \"FilesByMachines\" AS \"f9\"\n              WHERE \"m4\".\"Id\" = \"f9\".\"MachineId\") = 0)) AND EXISTS (\n              SELECT 1\n              FROM \"DisksByMachines\" AS \"d8\"\n              INNER JOIN \"Disks\" AS \"d9\" ON \"d8\".\"DiskId\" = \"d9\".\"Id\"\n              WHERE (\"m4\".\"Id\" = \"d8\".\"MachineId\") AND NOT (\"d9\".\"IsInRepo\"))))) + (\n          SELECT COUNT(*)\n          FROM \"Machines\" AS \"m5\"\n          WHERE (\"r\".\"Id\" = \"m5\".\"RomSetId\") AND ((((\n              SELECT COUNT(*)\n              FROM \"FilesByMachines\" AS \"f10\"\n              WHERE \"m5\".\"Id\" = \"f10\".\"MachineId\") > 0) AND ((\n              SELECT COUNT(*)\n              FROM \"DisksByMachines\" AS \"d10\"\n              WHERE \"m5\".\"Id\" = \"d10\".\"MachineId\") > 0)) AND (EXISTS (\n              SELECT 1\n              FROM \"FilesByMachines\" AS \"f11\"\n              INNER JOIN \"Files\" AS \"f12\" ON \"f11\".\"FileId\" = \"f12\".\"Id\"\n              WHERE (\"m5\".\"Id\" = \"f11\".\"MachineId\") AND NOT (\"f12\".\"IsInRepo\")) OR EXISTS (\n              SELECT 1\n              FROM \"DisksByMachines\" AS \"d11\"\n              INNER JOIN \"Disks\" AS \"d12\" ON \"d11\".\"DiskId\" = \"d12\".\"Id\"\n              WHERE (\"m5\".\"Id\" = \"d11\".\"MachineId\") AND NOT (\"d12\".\"IsInRepo\")))))) AS INTEGER) AS \"IncompleteMachines\", CAST((((\n          SELECT SUM((\n              SELECT COUNT(*)\n              FROM \"FilesByMachines\" AS \"f13\"\n              WHERE \"m6\".\"Id\" = \"f13\".\"MachineId\"))\n          FROM \"Machines\" AS \"m6\"\n          WHERE \"r\".\"Id\" = \"m6\".\"RomSetId\") + (\n          SELECT SUM((\n              SELECT COUNT(*)\n              FROM \"DisksByMachines\" AS \"d13\"\n              WHERE \"m7\".\"Id\" = \"d13\".\"MachineId\"))\n          FROM \"Machines\" AS \"m7\"\n          WHERE \"r\".\"Id\" = \"m7\".\"RomSetId\")) + (\n          SELECT SUM((\n              SELECT COUNT(*)\n              FROM \"MediasByMachines\" AS \"m8\"\n              WHERE \"m9\".\"Id\" = \"m8\".\"MachineId\"))\n          FROM \"Machines\" AS \"m9\"\n          WHERE \"r\".\"Id\" = \"m9\".\"RomSetId\")) AS INTEGER) AS \"TotalRoms\", CAST((((\n          SELECT SUM((\n              SELECT COUNT(*)\n              FROM \"FilesByMachines\" AS \"f14\"\n              INNER JOIN \"Files\" AS \"f15\" ON \"f14\".\"FileId\" = \"f15\".\"Id\"\n              WHERE (\"m10\".\"Id\" = \"f14\".\"MachineId\") AND \"f15\".\"IsInRepo\"))\n          FROM \"Machines\" AS \"m10\"\n          WHERE \"r\".\"Id\" = \"m10\".\"RomSetId\") + (\n          SELECT SUM((\n              SELECT COUNT(*)\n              FROM \"DisksByMachines\" AS \"d14\"\n              INNER JOIN \"Disks\" AS \"d15\" ON \"d14\".\"DiskId\" = \"d15\".\"Id\"\n              WHERE (\"m11\".\"Id\" = \"d14\".\"MachineId\") AND \"d15\".\"IsInRepo\"))\n          FROM \"Machines\" AS \"m11\"\n          WHERE \"r\".\"Id\" = \"m11\".\"RomSetId\")) + (\n          SELECT SUM((\n              SELECT COUNT(*)\n              FROM \"MediasByMachines\" AS \"m12\"\n              INNER JOIN \"Medias\" AS \"m13\" ON \"m12\".\"MediaId\" = \"m13\".\"Id\"\n              WHERE (\"m14\".\"Id\" = \"m12\".\"MachineId\") AND \"m13\".\"IsInRepo\"))\n          FROM \"Machines\" AS \"m14\"\n          WHERE \"r\".\"Id\" = \"m14\".\"RomSetId\")) AS INTEGER) AS \"HaveRoms\", CAST((((\n          SELECT SUM((\n              SELECT COUNT(*)\n              FROM \"FilesByMachines\" AS \"f16\"\n              INNER JOIN \"Files\" AS \"f17\" ON \"f16\".\"FileId\" = \"f17\".\"Id\"\n              WHERE (\"m15\".\"Id\" = \"f16\".\"MachineId\") AND NOT (\"f17\".\"IsInRepo\")))\n          FROM \"Machines\" AS \"m15\"\n          WHERE \"r\".\"Id\" = \"m15\".\"RomSetId\") + (\n          SELECT SUM((\n              SELECT COUNT(*)\n              FROM \"DisksByMachines\" AS \"d16\"\n              INNER JOIN \"Disks\" AS \"d17\" ON \"d16\".\"DiskId\" = \"d17\".\"Id\"\n              WHERE (\"m16\".\"Id\" = \"d16\".\"MachineId\") AND NOT (\"d17\".\"IsInRepo\")))\n          FROM \"Machines\" AS \"m16\"\n          WHERE \"r\".\"Id\" = \"m16\".\"RomSetId\")) + (\n          SELECT SUM((\n              SELECT COUNT(*)\n              FROM \"MediasByMachines\" AS \"m17\"\n              INNER JOIN \"Medias\" AS \"m18\" ON \"m17\".\"MediaId\" = \"m18\".\"Id\"\n              WHERE (\"m19\".\"Id\" = \"m17\".\"MachineId\") AND NOT (\"m18\".\"IsInRepo\")))\n          FROM \"Machines\" AS \"m19\"\n          WHERE \"r\".\"Id\" = \"m19\".\"RomSetId\")) AS INTEGER) AS \"MissRoms\"\n      FROM \"RomSets\" AS \"r\" WHERE \"TotalMachines\" > 0\n      ORDER BY \"r\".\"Id\"");
        }

        protected override void Down(MigrationBuilder migrationBuilder) => migrationBuilder.DropTable("RomSetStats");
    }
}