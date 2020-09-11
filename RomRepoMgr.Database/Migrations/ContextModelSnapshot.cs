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

            modelBuilder.Entity("RomRepoMgr.Database.Models.DbDisk", b =>
            {
                b.Property<ulong>("Id").ValueGeneratedOnAdd().HasColumnType("INTEGER");

                b.Property<DateTime>("CreatedOn").HasColumnType("TEXT");

                b.Property<bool>("IsInRepo").HasColumnType("INTEGER");

                b.Property<string>("Md5").HasColumnType("TEXT").HasMaxLength(32);

                b.Property<string>("OriginalFileName").HasColumnType("TEXT");

                b.Property<string>("Sha1").HasColumnType("TEXT").HasMaxLength(40);

                b.Property<ulong?>("Size").HasColumnType("INTEGER");

                b.Property<DateTime>("UpdatedOn").HasColumnType("TEXT");

                b.HasKey("Id");

                b.HasIndex("IsInRepo");

                b.HasIndex("Md5");

                b.HasIndex("Sha1");

                b.HasIndex("Size");

                b.ToTable("Disks");
            });

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

                b.HasIndex("IsInRepo");

                b.HasIndex("Md5");

                b.HasIndex("Sha1");

                b.HasIndex("Sha256");

                b.HasIndex("Sha384");

                b.HasIndex("Sha512");

                b.HasIndex("Size");

                b.HasIndex("Crc32", "Size");

                b.HasIndex("Md5", "Size");

                b.HasIndex("Sha1", "Size");

                b.HasIndex("Sha256", "Size");

                b.HasIndex("Sha384", "Size");

                b.HasIndex("Sha512", "Size");

                b.ToTable("Files");
            });

            modelBuilder.Entity("RomRepoMgr.Database.Models.DbMedia", b =>
            {
                b.Property<ulong>("Id").ValueGeneratedOnAdd().HasColumnType("INTEGER");

                b.Property<DateTime>("CreatedOn").HasColumnType("TEXT");

                b.Property<bool>("IsInRepo").HasColumnType("INTEGER");

                b.Property<string>("Md5").HasColumnType("TEXT").HasMaxLength(32);

                b.Property<string>("OriginalFileName").HasColumnType("TEXT");

                b.Property<string>("Sha1").HasColumnType("TEXT").HasMaxLength(40);

                b.Property<string>("Sha256").HasColumnType("TEXT").HasMaxLength(64);

                b.Property<ulong?>("Size").HasColumnType("INTEGER");

                b.Property<string>("SpamSum").HasColumnType("TEXT");

                b.Property<DateTime>("UpdatedOn").HasColumnType("TEXT");

                b.HasKey("Id");

                b.HasIndex("IsInRepo");

                b.HasIndex("Md5");

                b.HasIndex("Sha1");

                b.HasIndex("Sha256");

                b.HasIndex("Size");

                b.HasIndex("SpamSum");

                b.ToTable("Medias");
            });

            modelBuilder.Entity("RomRepoMgr.Database.Models.DiskByMachine", b =>
            {
                b.Property<ulong>("Id").ValueGeneratedOnAdd().HasColumnType("INTEGER");

                b.Property<ulong>("DiskId").HasColumnType("INTEGER");

                b.Property<ulong>("MachineId").HasColumnType("INTEGER");

                b.Property<string>("Name").IsRequired().HasColumnType("TEXT");

                b.HasKey("Id");

                b.HasIndex("DiskId");

                b.HasIndex("MachineId");

                b.HasIndex("Name");

                b.ToTable("DisksByMachines");
            });

            modelBuilder.Entity("RomRepoMgr.Database.Models.FileByMachine", b =>
            {
                b.Property<ulong>("Id").ValueGeneratedOnAdd().HasColumnType("INTEGER");

                b.Property<ulong>("FileId").HasColumnType("INTEGER");

                b.Property<DateTime?>("FileLastModification").HasColumnType("TEXT");

                b.Property<ulong>("MachineId").HasColumnType("INTEGER");

                b.Property<string>("Name").IsRequired().HasColumnType("TEXT");

                b.Property<string>("Path").HasColumnType("TEXT").HasMaxLength(4096);

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

            modelBuilder.Entity("RomRepoMgr.Database.Models.MediaByMachine", b =>
            {
                b.Property<ulong>("Id").ValueGeneratedOnAdd().HasColumnType("INTEGER");

                b.Property<ulong>("MachineId").HasColumnType("INTEGER");

                b.Property<ulong>("MediaId").HasColumnType("INTEGER");

                b.Property<string>("Name").IsRequired().HasColumnType("TEXT");

                b.HasKey("Id");

                b.HasIndex("MachineId");

                b.HasIndex("MediaId");

                b.HasIndex("Name");

                b.ToTable("MediasByMachines");
            });

            modelBuilder.Entity("RomRepoMgr.Database.Models.RomSet", b =>
            {
                b.Property<long>("Id").ValueGeneratedOnAdd().HasColumnType("INTEGER");

                b.Property<string>("Author").HasColumnType("TEXT");

                b.Property<string>("Category").HasColumnType("TEXT");

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

                b.HasIndex("Category");

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

            modelBuilder.Entity("RomRepoMgr.Database.Models.RomSetStat", b =>
            {
                b.Property<long>("RomSetId").HasColumnType("INTEGER");

                b.Property<long>("CompleteMachines").HasColumnType("INTEGER");

                b.Property<long>("HaveRoms").HasColumnType("INTEGER");

                b.Property<long>("IncompleteMachines").HasColumnType("INTEGER");

                b.Property<long>("MissRoms").HasColumnType("INTEGER");

                b.Property<long>("TotalMachines").HasColumnType("INTEGER");

                b.Property<long>("TotalRoms").HasColumnType("INTEGER");

                b.HasKey("RomSetId");

                b.ToTable("RomSetStats");
            });

            modelBuilder.Entity("RomRepoMgr.Database.Models.DiskByMachine", b =>
            {
                b.HasOne("RomRepoMgr.Database.Models.DbDisk", "Disk").WithMany("Machines").HasForeignKey("DiskId").
                  OnDelete(DeleteBehavior.Cascade).IsRequired();

                b.HasOne("RomRepoMgr.Database.Models.Machine", "Machine").WithMany("Disks").HasForeignKey("MachineId").
                  OnDelete(DeleteBehavior.Cascade).IsRequired();
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

            modelBuilder.Entity("RomRepoMgr.Database.Models.MediaByMachine", b =>
            {
                b.HasOne("RomRepoMgr.Database.Models.Machine", "Machine").WithMany("Medias").HasForeignKey("MachineId").
                  OnDelete(DeleteBehavior.Cascade).IsRequired();

                b.HasOne("RomRepoMgr.Database.Models.DbMedia", "Media").WithMany("Machines").HasForeignKey("MediaId").
                  OnDelete(DeleteBehavior.Cascade).IsRequired();
            });

            modelBuilder.Entity("RomRepoMgr.Database.Models.RomSetStat", b =>
            {
                b.HasOne("RomRepoMgr.Database.Models.RomSet", "RomSet").WithOne("Statistics").
                  HasForeignKey("RomRepoMgr.Database.Models.RomSetStat", "RomSetId").OnDelete(DeleteBehavior.Cascade).
                  IsRequired();
            });
            #pragma warning restore 612, 618
        }
    }
}