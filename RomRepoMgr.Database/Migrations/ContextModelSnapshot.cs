﻿// <auto-generated />

using System;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;

namespace RomRepoMgr.Database.Migrations
{
    [DbContext(typeof(Context))]
    internal class ContextModelSnapshot : ModelSnapshot
    {
        protected override void BuildModel(ModelBuilder modelBuilder)
        {
            #pragma warning disable 612, 618
            modelBuilder.HasAnnotation("ProductVersion", "3.1.7");

            modelBuilder.Entity("RomRepoMgr.Database.Models.DbFile", b =>
            {
                b.Property<ulong>("Id").ValueGeneratedOnAdd().HasColumnType("INTEGER");

                b.Property<string>("Crc32").HasColumnType("TEXT").HasMaxLength(8);

                b.Property<DateTime>("CreatedOn").HasColumnType("TEXT");

                b.Property<bool>("IsInRepo").HasColumnType("INTEGER");

                b.Property<string>("Md5").HasColumnType("TEXT").HasMaxLength(32);

                b.Property<string>("OriginalFileName").HasColumnType("TEXT");

                b.Property<string>("Sha1").HasColumnType("TEXT").HasMaxLength(40);

                b.Property<string>("Sha256").HasColumnType("TEXT").HasMaxLength(64);

                b.Property<string>("Sha384").HasColumnType("TEXT").HasMaxLength(96);

                b.Property<string>("Sha512").HasColumnType("TEXT").HasMaxLength(128);

                b.Property<ulong>("Size").HasColumnType("INTEGER");

                b.Property<DateTime>("UpdatedOn").HasColumnType("TEXT");

                b.HasKey("Id");

                b.HasIndex("Crc32");

                b.HasIndex("Sha1");

                b.HasIndex("Sha256");

                b.HasIndex("Sha384");

                b.HasIndex("Sha512");

                b.HasIndex("Size");

                b.ToTable("Files");
            });

            modelBuilder.Entity("RomRepoMgr.Database.Models.FileByMachine", b =>
            {
                b.Property<ulong>("Id").ValueGeneratedOnAdd().HasColumnType("INTEGER");

                b.Property<ulong>("FileId").HasColumnType("INTEGER");

                b.Property<ulong>("MachineId").HasColumnType("INTEGER");

                b.Property<string>("Name").IsRequired().HasColumnType("TEXT");

                b.HasKey("Id");

                b.HasIndex("FileId");

                b.HasIndex("MachineId");

                b.HasIndex("Name");

                b.ToTable("FilesByMachines");
            });

            modelBuilder.Entity("RomRepoMgr.Database.Models.Machine", b =>
            {
                b.Property<ulong>("Id").ValueGeneratedOnAdd().HasColumnType("INTEGER");

                b.Property<DateTime>("CreatedOn").HasColumnType("TEXT");

                b.Property<string>("Name").IsRequired().HasColumnType("TEXT");

                b.Property<long>("RomSetId").HasColumnType("INTEGER");

                b.Property<DateTime>("UpdatedOn").HasColumnType("TEXT");

                b.HasKey("Id");

                b.HasIndex("Name");

                b.HasIndex("RomSetId");

                b.ToTable("Machines");
            });

            modelBuilder.Entity("RomRepoMgr.Database.Models.RomSet", b =>
            {
                b.Property<long>("Id").ValueGeneratedOnAdd().HasColumnType("INTEGER");

                b.Property<string>("Author").HasColumnType("TEXT");

                b.Property<string>("Comment").HasColumnType("TEXT");

                b.Property<DateTime>("CreatedOn").HasColumnType("TEXT");

                b.Property<string>("Date").HasColumnType("TEXT");

                b.Property<string>("Description").HasColumnType("TEXT");

                b.Property<string>("Filename").IsRequired().HasColumnType("TEXT");

                b.Property<string>("Homepage").HasColumnType("TEXT");

                b.Property<string>("Name").HasColumnType("TEXT");

                b.Property<string>("Sha384").IsRequired().HasColumnType("TEXT").HasMaxLength(96);

                b.Property<DateTime>("UpdatedOn").HasColumnType("TEXT");

                b.Property<string>("Version").HasColumnType("TEXT");

                b.HasKey("Id");

                b.HasIndex("Author");

                b.HasIndex("Comment");

                b.HasIndex("Date");

                b.HasIndex("Description");

                b.HasIndex("Filename");

                b.HasIndex("Homepage");

                b.HasIndex("Name");

                b.HasIndex("Sha384");

                b.HasIndex("Version");

                b.ToTable("RomSets");
            });

            modelBuilder.Entity("RomRepoMgr.Database.Models.FileByMachine", b =>
            {
                b.HasOne("RomRepoMgr.Database.Models.DbFile", "File").WithMany("Machines").HasForeignKey("FileId").
                  OnDelete(DeleteBehavior.Cascade).IsRequired();

                b.HasOne("RomRepoMgr.Database.Models.Machine", "Machine").WithMany("Files").HasForeignKey("MachineId").
                  OnDelete(DeleteBehavior.Cascade).IsRequired();
            });

            modelBuilder.Entity("RomRepoMgr.Database.Models.Machine", b =>
            {
                b.HasOne("RomRepoMgr.Database.Models.RomSet", "RomSet").WithMany("Machines").HasForeignKey("RomSetId").
                  OnDelete(DeleteBehavior.Cascade).IsRequired();
            });
            #pragma warning restore 612, 618
        }
    }
}