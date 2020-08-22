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

using System;
using Microsoft.EntityFrameworkCore;
using RomRepoMgr.Database.Models;

namespace RomRepoMgr.Database
{
    public sealed class Context : DbContext
    {
        static Context _singleton;

        public Context(DbContextOptions options) : base(options) {}

        public static Context Singleton
        {
            get
            {
                if(_singleton != null)
                    return _singleton;

                if(Settings.Settings.Current?.DatabasePath is null)
                    throw new ArgumentNullException(nameof(Settings.Settings.Current.DatabasePath),
                                                    "Settings are not initialized!");

                _singleton = Create(Settings.Settings.Current.DatabasePath);

                return _singleton;
            }
        }

        public DbSet<DbFile>  Files    { get; set; }
        public DbSet<RomSet>  RomSets  { get; set; }
        public DbSet<Machine> Machines { get; set; }

        public static Context Create(string dbPath)
        {
            var optionsBuilder = new DbContextOptionsBuilder();
            optionsBuilder.UseLazyLoadingProxies().UseSqlite($"Data Source={dbPath}");

            return new Context(optionsBuilder.Options);
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<DbFile>(entity =>
            {
                entity.HasIndex(e => e.Crc32);

                entity.HasIndex(e => e.Sha1);

                entity.HasIndex(e => e.Sha256);

                entity.HasIndex(e => e.Sha384);

                entity.HasIndex(e => e.Sha512);

                entity.HasIndex(e => e.Size);
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
            });

            modelBuilder.Entity<Machine>(entity =>
            {
                entity.HasIndex(e => e.Name);

                entity.HasOne(e => e.RomSet).WithMany(e => e.Machines).OnDelete(DeleteBehavior.Cascade);
            });
        }
    }
}