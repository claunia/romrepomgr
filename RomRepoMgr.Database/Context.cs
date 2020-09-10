/******************************************************************************
// RomRepoMgr - ROM repository manager
// ----------------------------------------------------------------------------
//
// Author(s)      : Natalia Portillo <claunia@claunia.com>
//
// --[ License ] --------------------------------------------------------------
//
//     This program is free software: you can redistribute it and/or modify
//     it under the terms of the GNU General Public License as
//     published by the Free Software Foundation, either version 3 of the
//     License, or (at your option) any later version.
//
//     This program is distributed in the hope that it will be useful,
//     but WITHOUT ANY WARRANTY; without even the implied warranty of
//     MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
//     GNU General Public License for more details.
//
//     You should have received a copy of the GNU General Public License
//     along with this program.  If not, see <http://www.gnu.org/licenses/>.
//
// ----------------------------------------------------------------------------
// Copyright © 2020 Natalia Portillo
*******************************************************************************/

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using RomRepoMgr.Database.Models;

namespace RomRepoMgr.Database
{
    public sealed class Context : DbContext
    {
        public Context(DbContextOptions options) : base(options) {}

        public DbSet<DbFile>         Files            { get; set; }
        public DbSet<RomSet>         RomSets          { get; set; }
        public DbSet<Machine>        Machines         { get; set; }
        public DbSet<FileByMachine>  FilesByMachines  { get; set; }
        public DbSet<DbDisk>         Disks            { get; set; }
        public DbSet<DiskByMachine>  DisksByMachines  { get; set; }
        public DbSet<DbMedia>        Medias           { get; set; }
        public DbSet<MediaByMachine> MediasByMachines { get; set; }
        public DbSet<RomSetStat>     RomSetStats      { get; set; }

        public static Context Create(string dbPath)
        {
            var optionsBuilder = new DbContextOptionsBuilder();

            optionsBuilder.UseLazyLoadingProxies()
                       #if DEBUG
                          .UseLoggerFactory(LoggerFactory.Create(builder => builder.AddConsole()))
                       #endif
                          .UseSqlite($"Data Source={dbPath}");

            return new Context(optionsBuilder.Options);
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<DbFile>(entity =>
            {
                entity.HasIndex(e => e.Crc32);

                entity.HasIndex(e => e.Md5);

                entity.HasIndex(e => e.Sha1);

                entity.HasIndex(e => e.Sha256);

                entity.HasIndex(e => e.Sha384);

                entity.HasIndex(e => e.Sha512);

                entity.HasIndex(e => e.Size);

                entity.HasIndex(e => e.IsInRepo);
            });

            modelBuilder.Entity<RomSet>(entity =>
            {
                entity.HasIndex(e => e.Author);

                entity.HasIndex(e => e.Comment);

                entity.HasIndex(e => e.Date);

                entity.HasIndex(e => e.Description);

                entity.HasIndex(e => e.Homepage);

                entity.HasIndex(e => e.Name);

                entity.HasIndex(e => e.Version);

                entity.HasIndex(e => e.Filename);

                entity.HasIndex(e => e.Sha384);

                entity.HasIndex(e => e.Category);
            });

            modelBuilder.Entity<Machine>(entity =>
            {
                entity.HasIndex(e => e.Name);

                entity.HasOne(e => e.RomSet).WithMany(e => e.Machines).OnDelete(DeleteBehavior.Cascade);
            });

            modelBuilder.Entity<FileByMachine>(entity =>
            {
                entity.HasIndex(e => e.Name);

                entity.HasOne(e => e.Machine).WithMany(e => e.Files).OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(e => e.File).WithMany(e => e.Machines).OnDelete(DeleteBehavior.Cascade);
            });

            modelBuilder.Entity<DbDisk>(entity =>
            {
                entity.HasIndex(e => e.Md5);

                entity.HasIndex(e => e.Sha1);

                entity.HasIndex(e => e.Size);

                entity.HasIndex(e => e.IsInRepo);
            });

            modelBuilder.Entity<DiskByMachine>(entity =>
            {
                entity.HasIndex(e => e.Name);

                entity.HasOne(e => e.Machine).WithMany(e => e.Disks).OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(e => e.Disk).WithMany(e => e.Machines).OnDelete(DeleteBehavior.Cascade);
            });

            modelBuilder.Entity<DbMedia>(entity =>
            {
                entity.HasIndex(e => e.Md5);

                entity.HasIndex(e => e.Sha1);

                entity.HasIndex(e => e.Sha256);

                entity.HasIndex(e => e.SpamSum);

                entity.HasIndex(e => e.Size);

                entity.HasIndex(e => e.IsInRepo);
            });

            modelBuilder.Entity<MediaByMachine>(entity =>
            {
                entity.HasIndex(e => e.Name);

                entity.HasOne(e => e.Machine).WithMany(e => e.Medias).OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(e => e.Media).WithMany(e => e.Machines).OnDelete(DeleteBehavior.Cascade);
            });

            modelBuilder.Entity<RomSetStat>(entity =>
            {
                entity.HasOne(e => e.RomSet).WithOne(e => e.Statistics);
            });
        }
    }
}