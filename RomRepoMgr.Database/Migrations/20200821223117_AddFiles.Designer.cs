﻿// <auto-generated />
using System;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using RomRepoMgr.Database;

namespace RomRepoMgr.Database.Migrations
{
    [DbContext(typeof(Context))]
    [Migration("20200821223117_AddFiles")]
    partial class AddFiles
    {
        protected override void BuildTargetModel(ModelBuilder modelBuilder)
        {
#pragma warning disable 612, 618
            modelBuilder
                .HasAnnotation("ProductVersion", "3.1.7");

            modelBuilder.Entity("RomRepoMgr.Database.Models.DbFile", b =>
                {
                    b.Property<ulong>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("INTEGER");

                    b.Property<string>("Crc32")
                        .HasColumnType("TEXT")
                        .HasMaxLength(8);

                    b.Property<DateTime>("CreatedOn")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("TEXT");

                    b.Property<string>("Md5")
                        .HasColumnType("TEXT")
                        .HasMaxLength(32);

                    b.Property<string>("Sha1")
                        .HasColumnType("TEXT")
                        .HasMaxLength(40);

                    b.Property<string>("Sha256")
                        .HasColumnType("TEXT")
                        .HasMaxLength(64);

                    b.Property<ulong>("Size")
                        .HasColumnType("INTEGER");

                    b.Property<DateTime>("UpdatedOn")
                        .ValueGeneratedOnAddOrUpdate()
                        .HasColumnType("TEXT");

                    b.HasKey("Id");

                    b.HasIndex("Crc32");

                    b.HasIndex("Sha1");

                    b.HasIndex("Sha256");

                    b.HasIndex("Size");

                    b.ToTable("Files");
                });
#pragma warning restore 612, 618
        }
    }
}
