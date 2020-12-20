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
using System.Collections.Generic;
using System.Data.Common;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using Aaru.Checksums;
using EFCore.BulkExtensions;
using Microsoft.EntityFrameworkCore;
using RomRepoMgr.Core.EventArgs;
using RomRepoMgr.Core.Models;
using RomRepoMgr.Core.Resources;
using RomRepoMgr.Database;
using RomRepoMgr.Database.Models;
using SabreTools.DatFiles;
using SabreTools.DatItems;
using SabreTools.IO;
using ErrorEventArgs = RomRepoMgr.Core.EventArgs.ErrorEventArgs;
using Machine = RomRepoMgr.Database.Models.Machine;

namespace RomRepoMgr.Core.Workers
{
    public sealed class DatImporter
    {
        readonly string _category;
        readonly string _datFilesPath;
        readonly string _datPath;
        bool            _aborted;

        public DatImporter(string datPath, string category)
        {
            _datPath      = datPath;
            _datFilesPath = Path.Combine(Settings.Settings.Current.RepositoryPath, "datfiles");

            if(!string.IsNullOrWhiteSpace(category))
                _category = category;
        }

        public void Import()
        {
            try
            {
                using var ctx = Context.Create(Settings.Settings.Current.DatabasePath);

                SetIndeterminateProgress?.Invoke(this, System.EventArgs.Empty);

                SetMessage?.Invoke(this, new MessageEventArgs
                {
                    Message = Localization.ParsinDatFile
                });

                var datFile = DatFile.Create();
                datFile.ParseFile(_datPath, 0, false, throwOnError: true);

                SetMessage?.Invoke(this, new MessageEventArgs
                {
                    Message = Localization.HashingDatFile
                });

                string datHash = Sha384Context.File(_datPath, out byte[] datHashBinary);

                string datHash32 = Base32.ToBase32String(datHashBinary);

                if(!Directory.Exists(_datFilesPath))
                    Directory.CreateDirectory(_datFilesPath);

                string compressedDatPath = Path.Combine(_datFilesPath, datHash32 + ".lz");

                if(File.Exists(compressedDatPath))
                {
                    ErrorOccurred?.Invoke(this, new ErrorEventArgs
                    {
                        Message = Localization.DatAlreadyInDatabase
                    });

                    return;
                }

                SetMessage?.Invoke(this, new MessageEventArgs
                {
                    Message = Localization.AddingDatToDatabase
                });

                // TODO: Check if there is a hash in database but not in repo

                var romSet = new RomSet
                {
                    Author      = datFile.Header.Author,
                    Comment     = datFile.Header.Comment,
                    Date        = datFile.Header.Date,
                    Description = datFile.Header.Description,
                    Filename    = Path.GetFileName(_datPath),
                    Homepage    = datFile.Header.Homepage,
                    Name        = datFile.Header.Name,
                    Sha384      = datHash,
                    Version     = datFile.Header.Version,
                    CreatedOn   = DateTime.UtcNow,
                    UpdatedOn   = DateTime.UtcNow,
                    Category    = _category
                };

                ctx.RomSets.Add(romSet);
                ctx.SaveChanges();

                SetMessage?.Invoke(this, new MessageEventArgs
                {
                    Message = Localization.CompressingDatFile
                });

                var datCompress = new Compression();
                datCompress.SetProgress       += SetProgress;
                datCompress.SetProgressBounds += SetProgressBounds;
                datCompress.CompressFile(_datPath, compressedDatPath);

                SetMessage?.Invoke(this, new MessageEventArgs
                {
                    Message = Localization.GettingMachineNames
                });

                List<string> machineNames =
                    (from value in datFile.Items.Values from item in value select item.Machine.Name).Distinct().
                    ToList();

                SetMessage?.Invoke(this, new MessageEventArgs
                {
                    Message = Localization.AddingMachines
                });

                SetProgressBounds?.Invoke(this, new ProgressBoundsEventArgs
                {
                    Minimum = 0,
                    Maximum = machineNames.Count
                });

                int                         position = 0;
                Dictionary<string, Machine> machines = new Dictionary<string, Machine>();

                foreach(string name in machineNames)
                {
                    SetProgress?.Invoke(this, new ProgressEventArgs
                    {
                        Value = position
                    });

                    var machine = new Machine
                    {
                        Name      = name,
                        RomSetId  = romSet.Id,
                        CreatedOn = DateTime.UtcNow,
                        UpdatedOn = DateTime.UtcNow
                    };

                    machines[name] = machine;

                    position++;
                }

                SetMessage?.Invoke(this, new MessageEventArgs
                {
                    Message = Localization.SavingChangesToDatabase
                });

                SetIndeterminateProgress?.Invoke(this, System.EventArgs.Empty);

                ctx.BulkInsert(machines.Values.ToList(), b => b.SetOutputIdentity = true);
                ctx.SaveChanges();

                SetMessage?.Invoke(this, new MessageEventArgs
                {
                    Message = Localization.RetrievingRomsAndDisks
                });

                List<Rom>   roms   = new List<Rom>();
                List<Disk>  disks  = new List<Disk>();
                List<Media> medias = new List<Media>();

                string tmpRomCrc32Table    = Guid.NewGuid().ToString();
                string tmpRomMd5Table      = Guid.NewGuid().ToString();
                string tmpRomSha1Table     = Guid.NewGuid().ToString();
                string tmpRomSha256Table   = Guid.NewGuid().ToString();
                string tmpRomSha384Table   = Guid.NewGuid().ToString();
                string tmpRomSha512Table   = Guid.NewGuid().ToString();
                string tmpDiskMd5Table     = Guid.NewGuid().ToString();
                string tmpDiskSha1Table    = Guid.NewGuid().ToString();
                string tmpMediaMd5Table    = Guid.NewGuid().ToString();
                string tmpMediaSha1Table   = Guid.NewGuid().ToString();
                string tmpMediaSha256Table = Guid.NewGuid().ToString();

                bool romsHaveCrc      = false;
                bool romsHaveMd5      = false;
                bool romsHaveSha1     = false;
                bool romsHaveSha256   = false;
                bool romsHaveSha384   = false;
                bool romsHaveSha512   = false;
                bool disksHaveMd5     = false;
                bool disksHaveSha1    = false;
                bool mediasHaveMd5    = false;
                bool mediasHaveSha1   = false;
                bool mediasHaveSha256 = false;

                DbConnection dbConnection = ctx.Database.GetDbConnection();
                dbConnection.Open();

                position = 0;

                SetProgressBounds?.Invoke(this, new ProgressBoundsEventArgs
                {
                    Minimum = 0,
                    Maximum = datFile.Items.Values.Count
                });

                using(DbTransaction dbTransaction = dbConnection.BeginTransaction())
                {
                    DbCommand dbcc = dbConnection.CreateCommand();

                    dbcc.CommandText =
                        $"CREATE TABLE \"{tmpRomCrc32Table}\" (\"Size\" INTEGER NOT NULL, \"Crc32\" TEXT NOT NULL);";

                    dbcc.ExecuteNonQuery();
                    dbcc = dbConnection.CreateCommand();

                    dbcc.CommandText =
                        $"CREATE TABLE \"{tmpRomMd5Table}\" (\"Size\" INTEGER NOT NULL, \"Md5\" TEXT NOT NULL);";

                    dbcc.ExecuteNonQuery();
                    dbcc = dbConnection.CreateCommand();

                    dbcc.CommandText =
                        $"CREATE TABLE \"{tmpRomSha1Table}\" (\"Size\" INTEGER NOT NULL, \"Sha1\" TEXT NOT NULL);";

                    dbcc.ExecuteNonQuery();
                    dbcc = dbConnection.CreateCommand();

                    dbcc.CommandText =
                        $"CREATE TABLE \"{tmpRomSha256Table}\" (\"Size\" INTEGER NOT NULL, \"Sha256\" TEXT NOT NULL);";

                    dbcc.ExecuteNonQuery();
                    dbcc = dbConnection.CreateCommand();

                    dbcc.CommandText =
                        $"CREATE TABLE \"{tmpRomSha384Table}\" (\"Size\" INTEGER NOT NULL, \"Sha384\" TEXT NOT NULL);";

                    dbcc.ExecuteNonQuery();
                    dbcc = dbConnection.CreateCommand();

                    dbcc.CommandText =
                        $"CREATE TABLE \"{tmpRomSha512Table}\" (\"Size\" INTEGER NOT NULL, \"Sha512\" TEXT NOT NULL);";

                    dbcc.ExecuteNonQuery();
                    dbcc             = dbConnection.CreateCommand();
                    dbcc.CommandText = $"CREATE TABLE \"{tmpDiskMd5Table}\" (\"Md5\" TEXT NOT NULL);";
                    dbcc.ExecuteNonQuery();
                    dbcc             = dbConnection.CreateCommand();
                    dbcc.CommandText = $"CREATE TABLE \"{tmpDiskSha1Table}\" (\"Sha1\" TEXT NOT NULL);";
                    dbcc.ExecuteNonQuery();
                    dbcc             = dbConnection.CreateCommand();
                    dbcc.CommandText = $"CREATE TABLE \"{tmpMediaMd5Table}\" (\"Md5\" TEXT NOT NULL);";
                    dbcc.ExecuteNonQuery();
                    dbcc             = dbConnection.CreateCommand();
                    dbcc.CommandText = $"CREATE TABLE \"{tmpMediaSha1Table}\" (\"Sha1\" TEXT NOT NULL);";
                    dbcc.ExecuteNonQuery();
                    dbcc             = dbConnection.CreateCommand();
                    dbcc.CommandText = $"CREATE TABLE \"{tmpMediaSha256Table}\" (\"Sha256\" TEXT NOT NULL);";
                    dbcc.ExecuteNonQuery();

                    foreach(List<DatItem> values in datFile.Items.Values)
                    {
                        SetProgress?.Invoke(this, new ProgressEventArgs
                        {
                            Value = position
                        });

                        foreach(DatItem item in values)
                        {
                            switch(item)
                            {
                                case Rom rom:
                                    if(rom.CRC != null)
                                    {
                                        dbcc = dbConnection.CreateCommand();

                                        dbcc.CommandText =
                                            $"INSERT INTO \"{tmpRomCrc32Table}\" (\"Size\", \"Crc32\") VALUES (\"{(ulong)rom.Size}\", \"{rom.CRC}\");";

                                        dbcc.ExecuteNonQuery();

                                        romsHaveCrc = true;
                                    }

                                    if(rom.MD5 != null)
                                    {
                                        dbcc = dbConnection.CreateCommand();

                                        dbcc.CommandText =
                                            $"INSERT INTO \"{tmpRomMd5Table}\" (\"Size\", \"Md5\") VALUES (\"{(ulong)rom.Size}\", \"{rom.MD5}\");";

                                        dbcc.ExecuteNonQuery();

                                        romsHaveMd5 = true;
                                    }

                                    if(rom.SHA1 != null)
                                    {
                                        dbcc = dbConnection.CreateCommand();

                                        dbcc.CommandText =
                                            $"INSERT INTO \"{tmpRomSha1Table}\" (\"Size\", \"Sha1\") VALUES (\"{(ulong)rom.Size}\", \"{rom.SHA1}\");";

                                        dbcc.ExecuteNonQuery();

                                        romsHaveSha1 = true;
                                    }

                                    if(rom.SHA256 != null)
                                    {
                                        dbcc = dbConnection.CreateCommand();

                                        dbcc.CommandText =
                                            $"INSERT INTO \"{tmpRomSha256Table}\" (\"Size\", \"Sha256\") VALUES (\"{(ulong)rom.Size}\", \"{rom.SHA256}\");";

                                        dbcc.ExecuteNonQuery();

                                        romsHaveSha256 = true;
                                    }

                                    if(rom.SHA384 != null)
                                    {
                                        dbcc = dbConnection.CreateCommand();

                                        dbcc.CommandText =
                                            $"INSERT INTO \"{tmpRomSha384Table}\" (\"Size\", \"Sha384\") VALUES (\"{(ulong)rom.Size}\", \"{rom.SHA384}\");";

                                        dbcc.ExecuteNonQuery();

                                        romsHaveSha384 = true;
                                    }

                                    if(rom.SHA512 != null)
                                    {
                                        dbcc = dbConnection.CreateCommand();

                                        dbcc.CommandText =
                                            $"INSERT INTO \"{tmpRomSha512Table}\" (\"Size\", \"Sha512\") VALUES (\"{(ulong)rom.Size}\", \"{rom.SHA512}\");";

                                        dbcc.ExecuteNonQuery();

                                        romsHaveSha512 = true;
                                    }

                                    roms.Add(rom);

                                    continue;
                                case Disk disk:
                                    if(disk.MD5 != null)
                                    {
                                        dbcc = dbConnection.CreateCommand();

                                        dbcc.CommandText =
                                            $"INSERT INTO \"{tmpDiskMd5Table}\" (\"Md5\") VALUES (\"{disk.MD5}\");";

                                        dbcc.ExecuteNonQuery();

                                        disksHaveMd5 = true;
                                    }

                                    if(disk.SHA1 != null)
                                    {
                                        dbcc = dbConnection.CreateCommand();

                                        dbcc.CommandText =
                                            $"INSERT INTO \"{tmpDiskSha1Table}\" (\"Sha1\") VALUES (\"{disk.SHA1}\");";

                                        dbcc.ExecuteNonQuery();

                                        disksHaveSha1 = true;
                                    }

                                    disks.Add(disk);

                                    continue;
                                case Media media:
                                    if(media.MD5 != null)
                                    {
                                        dbcc = dbConnection.CreateCommand();

                                        dbcc.CommandText =
                                            $"INSERT INTO \"{tmpMediaMd5Table}\" (\"Md5\") VALUES (\"{media.MD5}\");";

                                        dbcc.ExecuteNonQuery();

                                        mediasHaveMd5 = true;
                                    }

                                    if(media.SHA1 != null)
                                    {
                                        dbcc = dbConnection.CreateCommand();

                                        dbcc.CommandText =
                                            $"INSERT INTO \"{tmpMediaSha1Table}\" (\"Sha1\") VALUES (\"{media.SHA1}\");";

                                        dbcc.ExecuteNonQuery();

                                        mediasHaveSha1 = true;
                                    }

                                    if(media.SHA256 != null)
                                    {
                                        dbcc = dbConnection.CreateCommand();

                                        dbcc.CommandText =
                                            $"INSERT INTO \"{tmpMediaSha256Table}\" (\"Sha256\") VALUES (\"{media.SHA256}\");";

                                        dbcc.ExecuteNonQuery();

                                        mediasHaveSha256 = true;
                                    }

                                    medias.Add(media);

                                    continue;
                            }
                        }

                        position++;
                    }

                    SetIndeterminateProgress?.Invoke(this, System.EventArgs.Empty);

                    dbTransaction.Commit();
                }

                List<DbFile> pendingFilesByCrcList = romsHaveCrc ? ctx.Files.
                                                                       FromSqlRaw($"SELECT DISTINCT f.* FROM Files AS f, [{tmpRomCrc32Table}] AS t WHERE f.Crc32 = t.Crc32 AND f.Size = t.Size").
                                                                       ToList() : new List<DbFile>();

                List<DbFile> pendingFilesByMd5List = romsHaveMd5 ? ctx.Files.
                                                                       FromSqlRaw($"SELECT DISTINCT f.* FROM Files AS f, [{tmpRomMd5Table}] AS t WHERE f.Md5 = t.Md5 AND f.Size = t.Size").
                                                                       ToList() : new List<DbFile>();

                List<DbFile> pendingFilesBySha1List = romsHaveSha1 ? ctx.Files.
                                                                         FromSqlRaw($"SELECT DISTINCT f.* FROM Files AS f, [{tmpRomSha1Table}] AS t WHERE f.Sha1 = t.Sha1 AND f.Size = t.Size").
                                                                         ToList() : new List<DbFile>();

                List<DbFile> pendingFilesBySha256List = romsHaveSha256 ? ctx.Files.
                                                                             FromSqlRaw($"SELECT DISTINCT f.* FROM Files AS f, [{tmpRomSha256Table}] AS t WHERE f.Sha256 = t.Sha256 AND f.Size = t.Size").
                                                                             ToList() : new List<DbFile>();

                List<DbFile> pendingFilesBySha384List = romsHaveSha384 ? ctx.Files.
                                                                             FromSqlRaw($"SELECT DISTINCT f.* FROM Files AS f, [{tmpRomSha384Table}] AS t WHERE f.Sha384 = t.Sha384 AND f.Size = t.Size").
                                                                             ToList() : new List<DbFile>();

                List<DbFile> pendingFilesBySha512List = romsHaveSha512 ? ctx.Files.
                                                                             FromSqlRaw($"SELECT DISTINCT f.* FROM Files AS f, [{tmpRomSha512Table}] AS t WHERE f.Sha512 = t.Sha512 AND f.Size = t.Size").
                                                                             ToList() : new List<DbFile>();

                Dictionary<string, DbDisk> pendingDisksByMd5 = disksHaveMd5 ? ctx.Disks.
                                                                       FromSqlRaw($"SELECT DISTINCT f.* FROM Disks AS f, [{tmpDiskMd5Table}] AS t WHERE f.Md5 = t.Md5").
                                                                       ToDictionary(f => f.Md5)
                                                                   : new Dictionary<string, DbDisk>();

                Dictionary<string, DbDisk> pendingDisksBySha1 = disksHaveSha1 ? ctx.Disks.
                                                                        FromSqlRaw($"SELECT DISTINCT f.* FROM Disks AS f, [{tmpDiskSha1Table}] AS t WHERE f.Sha1 = t.Sha1").
                                                                        ToDictionary(f => f.Sha1)
                                                                    : new Dictionary<string, DbDisk>();

                Dictionary<string, DbMedia> pendingMediasByMd5 = mediasHaveMd5 ? ctx.Medias.
                                                                         FromSqlRaw($"SELECT DISTINCT f.* FROM Medias AS f, [{tmpMediaMd5Table}] AS t WHERE f.Md5 = t.Md5").
                                                                         ToDictionary(f => f.Md5)
                                                                     : new Dictionary<string, DbMedia>();

                Dictionary<string, DbMedia> pendingMediasBySha1 = mediasHaveSha1 ? ctx.Medias.
                                                                          FromSqlRaw($"SELECT DISTINCT f.* FROM Medias AS f, [{tmpMediaSha1Table}] AS t WHERE f.Sha1 = t.Sha1").
                                                                          ToDictionary(f => f.Sha1)
                                                                      : new Dictionary<string, DbMedia>();

                Dictionary<string, DbMedia> pendingMediasBySha256 = mediasHaveSha256 ? ctx.Medias.
                                                                            FromSqlRaw($"SELECT DISTINCT f.* FROM Medias AS f, [{tmpMediaSha256Table}] AS t WHERE f.Sha256 = t.Sha256").
                                                                            ToDictionary(f => f.Sha256)
                                                                        : new Dictionary<string, DbMedia>();

                Dictionary<string, DbFile> pendingFilesByCrc    = new Dictionary<string, DbFile>();
                Dictionary<string, DbFile> pendingFilesByMd5    = new Dictionary<string, DbFile>();
                Dictionary<string, DbFile> pendingFilesBySha1   = new Dictionary<string, DbFile>();
                Dictionary<string, DbFile> pendingFilesBySha256 = new Dictionary<string, DbFile>();
                Dictionary<string, DbFile> pendingFilesBySha384 = new Dictionary<string, DbFile>();
                Dictionary<string, DbFile> pendingFilesBySha512 = new Dictionary<string, DbFile>();
                List<DbFile>               pendingFiles         = new List<DbFile>();

                // This is because of hash collisions.
                foreach(DbFile item in pendingFilesByCrcList)
                    if(pendingFilesByCrc.ContainsKey(item.Crc32))
                        pendingFiles.Add(item);
                    else
                        pendingFilesByCrc[item.Crc32] = item;

                foreach(DbFile item in pendingFilesByMd5List)
                    if(pendingFilesByMd5.ContainsKey(item.Md5))
                        pendingFiles.Add(item);
                    else
                        pendingFilesByMd5[item.Md5] = item;

                foreach(DbFile item in pendingFilesBySha1List)
                    if(pendingFilesBySha1.ContainsKey(item.Sha1))
                        pendingFiles.Add(item);
                    else
                        pendingFilesBySha1[item.Sha1] = item;

                foreach(DbFile item in pendingFilesBySha256List)
                    if(pendingFilesBySha256.ContainsKey(item.Sha256))
                        pendingFiles.Add(item);
                    else
                        pendingFilesBySha256[item.Sha256] = item;

                foreach(DbFile item in pendingFilesBySha384List)
                    if(pendingFilesBySha384.ContainsKey(item.Sha384))
                        pendingFiles.Add(item);
                    else
                        pendingFilesBySha384[item.Sha384] = item;

                foreach(DbFile item in pendingFilesBySha512List)
                    if(pendingFilesBySha512.ContainsKey(item.Sha512))
                        pendingFiles.Add(item);
                    else
                        pendingFilesBySha512[item.Sha512] = item;

                // Clear some memory
                pendingFilesByCrcList.Clear();
                pendingFilesByMd5List.Clear();
                pendingFilesBySha1List.Clear();
                pendingFilesBySha256List.Clear();
                pendingFilesBySha384List.Clear();
                pendingFilesBySha512List.Clear();

                ctx.Database.ExecuteSqlRaw($"DROP TABLE [{tmpRomCrc32Table}]");
                ctx.Database.ExecuteSqlRaw($"DROP TABLE [{tmpRomMd5Table}]");
                ctx.Database.ExecuteSqlRaw($"DROP TABLE [{tmpRomSha1Table}]");
                ctx.Database.ExecuteSqlRaw($"DROP TABLE [{tmpRomSha256Table}]");
                ctx.Database.ExecuteSqlRaw($"DROP TABLE [{tmpRomSha384Table}]");
                ctx.Database.ExecuteSqlRaw($"DROP TABLE [{tmpRomSha512Table}]");
                ctx.Database.ExecuteSqlRaw($"DROP TABLE [{tmpDiskMd5Table}]");
                ctx.Database.ExecuteSqlRaw($"DROP TABLE [{tmpDiskSha1Table}]");
                ctx.Database.ExecuteSqlRaw($"DROP TABLE [{tmpMediaMd5Table}]");
                ctx.Database.ExecuteSqlRaw($"DROP TABLE [{tmpMediaSha1Table}]");
                ctx.Database.ExecuteSqlRaw($"DROP TABLE [{tmpMediaSha256Table}]");

                SetProgressBounds?.Invoke(this, new ProgressBoundsEventArgs
                {
                    Minimum = 0,
                    Maximum = roms.Count
                });

                SetMessage?.Invoke(this, new MessageEventArgs
                {
                    Message = Localization.AddingRoms
                });

                position = 0;

                List<DbFile>        newFiles          = new List<DbFile>();
                List<FileByMachine> newFilesByMachine = new List<FileByMachine>();

                foreach(Rom rom in roms)
                {
                    bool hashCollision = false;

                    SetProgress?.Invoke(this, new ProgressEventArgs
                    {
                        Value = position
                    });

                    if(!machines.TryGetValue(rom.Machine.Name, out Machine machine))
                    {
                        ErrorOccurred?.Invoke(this, new ErrorEventArgs
                        {
                            Message = Localization.FoundRomWithoutMachine
                        });

                        return;
                    }

                    ulong uSize = (ulong)rom.Size;

                    DbFile file = null;

                    if(rom.SHA512 != null)
                        if(pendingFilesBySha512.TryGetValue(rom.SHA512, out file))
                            if(file.Size != uSize)
                            {
                                hashCollision = true;
                                file          = null;
                            }

                    if(rom.SHA384 != null &&
                       file       == null)
                        if(pendingFilesBySha384.TryGetValue(rom.SHA384, out file))
                            if(file.Size != uSize)
                            {
                                hashCollision = true;
                                file          = null;
                            }

                    if(rom.SHA256 != null &&
                       file       == null)
                        if(pendingFilesBySha256.TryGetValue(rom.SHA256, out file))
                            if(file.Size != uSize)
                            {
                                hashCollision = true;
                                file          = null;
                            }

                    if(rom.SHA1 != null &&
                       file     == null)
                        if(pendingFilesBySha1.TryGetValue(rom.SHA1, out file))
                            if(file.Size != uSize)
                            {
                                hashCollision = true;
                                file          = null;
                            }

                    if(rom.MD5 != null &&
                       file    == null)
                        if(pendingFilesByMd5.TryGetValue(rom.MD5, out file))
                            if(file.Size != uSize)
                            {
                                hashCollision = true;
                                file          = null;
                            }

                    if(rom.CRC != null &&
                       file    == null)
                        if(pendingFilesByCrc.TryGetValue(rom.CRC, out file))
                            if(file.Size != uSize)
                            {
                                hashCollision = true;
                                file          = null;
                            }

                    if(file == null && hashCollision)
                    {
                        if(rom.SHA512 != null)
                            file = pendingFiles.FirstOrDefault(f => f.Sha512 == rom.SHA512 && f.Size == uSize);

                        if(file       == null &&
                           rom.SHA384 != null)
                            file = pendingFiles.FirstOrDefault(f => f.Sha384 == rom.SHA384 && f.Size == uSize);

                        if(file       == null &&
                           rom.SHA256 != null)
                            file = pendingFiles.FirstOrDefault(f => f.Sha256 == rom.SHA256 && f.Size == uSize);

                        if(file     == null &&
                           rom.SHA1 != null)
                            file = pendingFiles.FirstOrDefault(f => f.Sha1 == rom.SHA1 && f.Size == uSize);

                        if(file    == null &&
                           rom.MD5 != null)
                            file = pendingFiles.FirstOrDefault(f => f.Md5 == rom.MD5 && f.Size == uSize);

                        if(file    == null &&
                           rom.CRC != null)
                            file = pendingFiles.FirstOrDefault(f => f.Crc32 == rom.CRC && f.Size == uSize);
                    }

                    if(file == null)
                    {
                        file = new DbFile
                        {
                            Crc32     = rom.CRC,
                            CreatedOn = DateTime.UtcNow,
                            Md5       = rom.MD5,
                            Sha1      = rom.SHA1,
                            Sha256    = rom.SHA256,
                            Sha384    = rom.SHA384,
                            Sha512    = rom.SHA512,
                            Size      = uSize,
                            UpdatedOn = DateTime.UtcNow
                        };

                        newFiles.Add(file);
                    }

                    if(string.IsNullOrEmpty(file.Crc32) &&
                       !string.IsNullOrEmpty(rom.CRC))
                    {
                        file.Crc32     = rom.CRC;
                        file.UpdatedOn = DateTime.UtcNow;
                    }

                    if(string.IsNullOrEmpty(file.Md5) &&
                       !string.IsNullOrEmpty(rom.MD5))
                    {
                        file.Md5       = rom.MD5;
                        file.UpdatedOn = DateTime.UtcNow;
                    }

                    if(string.IsNullOrEmpty(file.Sha1) &&
                       !string.IsNullOrEmpty(rom.SHA1))
                    {
                        file.Sha1      = rom.SHA1;
                        file.UpdatedOn = DateTime.UtcNow;
                    }

                    if(string.IsNullOrEmpty(file.Sha256) &&
                       !string.IsNullOrEmpty(rom.SHA256))
                    {
                        file.Sha256    = rom.SHA256;
                        file.UpdatedOn = DateTime.UtcNow;
                    }

                    if(string.IsNullOrEmpty(file.Sha384) &&
                       !string.IsNullOrEmpty(rom.SHA384))
                    {
                        file.Sha384    = rom.SHA384;
                        file.UpdatedOn = DateTime.UtcNow;
                    }

                    if(string.IsNullOrEmpty(file.Sha512) &&
                       !string.IsNullOrEmpty(rom.SHA512))
                    {
                        file.Sha512    = rom.SHA512;
                        file.UpdatedOn = DateTime.UtcNow;
                    }

                    DateTime? fileModificationDate = null;

                    if(!string.IsNullOrEmpty(rom.Date))
                    {
                        rom.Date = rom.Date.Replace("/", "\\");

                        if(DateTime.TryParseExact(rom.Date, @"yyyy\\M\\d H:mm", CultureInfo.InvariantCulture,
                                                  DateTimeStyles.AssumeUniversal, out DateTime date))
                            fileModificationDate = date;
                    }

                    string filename;
                    string path = null;

                    if(rom.Name.Contains('\\'))
                    {
                        filename = Path.GetFileName(rom.Name.Replace('\\', '/'));
                        path     = Path.GetDirectoryName(rom.Name.Replace('\\', '/'));
                    }
                    else if(rom.Name.Contains('/'))
                    {
                        filename = Path.GetFileName(rom.Name);
                        path     = Path.GetDirectoryName(rom.Name);
                    }
                    else
                        filename = rom.Name;

                    newFilesByMachine.Add(new FileByMachine
                    {
                        File                 = file,
                        Machine              = machine,
                        Name                 = filename,
                        FileLastModification = fileModificationDate,
                        Path                 = path
                    });

                    if(hashCollision)
                        pendingFiles.Add(file);
                    else if(file.Sha512 != null)
                        pendingFilesBySha512[file.Sha512] = file;
                    else if(file.Sha384 != null)
                        pendingFilesBySha384[file.Sha384] = file;
                    else if(file.Sha256 != null)
                        pendingFilesBySha256[file.Sha256] = file;
                    else if(file.Sha1 != null)
                        pendingFilesBySha1[file.Sha1] = file;
                    else if(file.Md5 != null)
                        pendingFilesByMd5[file.Md5] = file;
                    else if(file.Crc32 != null)
                        pendingFilesByCrc[file.Crc32] = file;

                    position++;
                }

                SetMessage?.Invoke(this, new MessageEventArgs
                {
                    Message = Localization.SavingChangesToDatabase
                });

                SetIndeterminateProgress?.Invoke(this, System.EventArgs.Empty);

                ctx.BulkInsert(newFiles, b => b.SetOutputIdentity = true);

                foreach(FileByMachine fbm in newFilesByMachine)
                {
                    fbm.FileId    = fbm.File.Id;
                    fbm.MachineId = fbm.Machine.Id;
                }

                ctx.BulkInsert(newFilesByMachine);

                ctx.SaveChanges();

                pendingFilesBySha512.Clear();
                pendingFilesBySha384.Clear();
                pendingFilesBySha256.Clear();
                pendingFilesBySha1.Clear();
                pendingFilesByMd5.Clear();
                pendingFilesByCrc.Clear();
                pendingFiles.Clear();
                newFiles.Clear();
                newFilesByMachine.Clear();

                SetProgressBounds?.Invoke(this, new ProgressBoundsEventArgs
                {
                    Minimum = 0,
                    Maximum = disks.Count
                });

                SetMessage?.Invoke(this, new MessageEventArgs
                {
                    Message = Localization.AddingDisks
                });

                position = 0;

                List<DbDisk>        newDisks          = new List<DbDisk>();
                List<DiskByMachine> newDisksByMachine = new List<DiskByMachine>();

                foreach(Disk disk in disks)
                {
                    SetProgress?.Invoke(this, new ProgressEventArgs
                    {
                        Value = position
                    });

                    if(!machines.TryGetValue(disk.Machine.Name, out Machine machine))
                    {
                        ErrorOccurred?.Invoke(this, new ErrorEventArgs
                        {
                            Message = Localization.FoundDiskWithoutMachine
                        });

                        return;
                    }

                    if(disk.MD5  == null &&
                       disk.SHA1 == null)
                    {
                        position++;

                        continue;
                    }

                    DbDisk dbDisk = null;

                    if(disk.SHA1 != null &&
                       dbDisk    == null)
                        pendingDisksBySha1.TryGetValue(disk.SHA1, out dbDisk);

                    if(disk.MD5 != null &&
                       dbDisk   == null)
                        pendingDisksByMd5.TryGetValue(disk.MD5, out dbDisk);

                    if(dbDisk == null)
                    {
                        dbDisk = new DbDisk
                        {
                            CreatedOn = DateTime.UtcNow,
                            Md5       = disk.MD5,
                            Sha1      = disk.SHA1,
                            UpdatedOn = DateTime.UtcNow
                        };

                        newDisks.Add(dbDisk);
                    }

                    if(string.IsNullOrEmpty(dbDisk.Md5) &&
                       !string.IsNullOrEmpty(disk.MD5))
                    {
                        dbDisk.Md5       = disk.MD5;
                        dbDisk.UpdatedOn = DateTime.UtcNow;
                    }

                    if(string.IsNullOrEmpty(dbDisk.Sha1) &&
                       !string.IsNullOrEmpty(disk.SHA1))
                    {
                        dbDisk.Sha1      = disk.SHA1;
                        dbDisk.UpdatedOn = DateTime.UtcNow;
                    }

                    newDisksByMachine.Add(new DiskByMachine
                    {
                        Disk    = dbDisk,
                        Machine = machine,
                        Name    = disk.Name
                    });

                    if(dbDisk.Sha1 != null)
                        pendingDisksBySha1[dbDisk.Sha1] = dbDisk;

                    if(dbDisk.Md5 != null)
                        pendingDisksByMd5[dbDisk.Md5] = dbDisk;

                    position++;
                }

                SetMessage?.Invoke(this, new MessageEventArgs
                {
                    Message = Localization.SavingChangesToDatabase
                });

                SetIndeterminateProgress?.Invoke(this, System.EventArgs.Empty);

                ctx.BulkInsert(newDisks, b => b.SetOutputIdentity = true);

                foreach(DiskByMachine dbm in newDisksByMachine)
                {
                    dbm.DiskId    = dbm.Disk.Id;
                    dbm.MachineId = dbm.Machine.Id;
                }

                ctx.BulkInsert(newDisksByMachine);

                ctx.SaveChanges();

                pendingDisksBySha1.Clear();
                pendingDisksByMd5.Clear();
                newDisks.Clear();
                newDisksByMachine.Clear();

                SetProgressBounds?.Invoke(this, new ProgressBoundsEventArgs
                {
                    Minimum = 0,
                    Maximum = medias.Count
                });

                SetMessage?.Invoke(this, new MessageEventArgs
                {
                    Message = Localization.AddingMedias
                });

                position = 0;

                List<DbMedia>        newMedias          = new List<DbMedia>();
                List<MediaByMachine> newMediasByMachine = new List<MediaByMachine>();

                foreach(Media media in medias)
                {
                    SetProgress?.Invoke(this, new ProgressEventArgs
                    {
                        Value = position
                    });

                    if(!machines.TryGetValue(media.Machine.Name, out Machine machine))
                    {
                        ErrorOccurred?.Invoke(this, new ErrorEventArgs
                        {
                            Message = Localization.FoundMediaWithoutMachine
                        });

                        return;
                    }

                    if(media.MD5    == null &&
                       media.SHA1   == null &&
                       media.SHA256 == null)
                    {
                        position++;

                        continue;
                    }

                    DbMedia dbMedia = null;

                    if(media.SHA256 != null &&
                       dbMedia      == null)
                        pendingMediasBySha256.TryGetValue(media.SHA256, out dbMedia);

                    if(media.SHA1 != null &&
                       dbMedia    == null)
                        pendingMediasBySha1.TryGetValue(media.SHA1, out dbMedia);

                    if(media.MD5 != null &&
                       dbMedia   == null)
                        pendingMediasByMd5.TryGetValue(media.MD5, out dbMedia);

                    // TODO: SpamSum
                    if(dbMedia == null)
                    {
                        dbMedia = new DbMedia
                        {
                            CreatedOn = DateTime.UtcNow,
                            Md5       = media.MD5,
                            Sha1      = media.SHA1,
                            Sha256    = media.SHA256,
                            UpdatedOn = DateTime.UtcNow
                        };

                        newMedias.Add(dbMedia);
                    }

                    if(string.IsNullOrEmpty(dbMedia.Md5) &&
                       !string.IsNullOrEmpty(media.MD5))
                    {
                        dbMedia.Md5       = media.MD5;
                        dbMedia.UpdatedOn = DateTime.UtcNow;
                    }

                    if(string.IsNullOrEmpty(dbMedia.Sha1) &&
                       !string.IsNullOrEmpty(media.SHA1))
                    {
                        dbMedia.Sha1      = media.SHA1;
                        dbMedia.UpdatedOn = DateTime.UtcNow;
                    }

                    if(string.IsNullOrEmpty(dbMedia.Sha256) &&
                       !string.IsNullOrEmpty(media.SHA256))
                    {
                        dbMedia.Sha256    = media.SHA256;
                        dbMedia.UpdatedOn = DateTime.UtcNow;
                    }

                    newMediasByMachine.Add(new MediaByMachine
                    {
                        Media   = dbMedia,
                        Machine = machine,
                        Name    = media.Name
                    });

                    if(dbMedia.Sha256 != null)
                        pendingMediasBySha256[dbMedia.Sha256] = dbMedia;

                    if(dbMedia.Sha1 != null)
                        pendingMediasBySha1[dbMedia.Sha1] = dbMedia;

                    if(dbMedia.Md5 != null)
                        pendingMediasByMd5[dbMedia.Md5] = dbMedia;

                    position++;
                }

                SetMessage?.Invoke(this, new MessageEventArgs
                {
                    Message = Localization.SavingChangesToDatabase
                });

                SetIndeterminateProgress?.Invoke(this, System.EventArgs.Empty);

                ctx.BulkInsert(newMedias, b => b.SetOutputIdentity = true);

                foreach(MediaByMachine mbm in newMediasByMachine)
                {
                    mbm.MediaId   = mbm.Media.Id;
                    mbm.MachineId = mbm.Machine.Id;
                }

                ctx.BulkInsert(newMediasByMachine);

                ctx.SaveChanges();

                pendingMediasBySha256.Clear();
                pendingMediasBySha1.Clear();
                pendingMediasByMd5.Clear();
                newMedias.Clear();
                newMediasByMachine.Clear();

                RomSetStat stats = ctx.RomSets.Where(r => r.Id == romSet.Id).Select(r => new RomSetStat
                {
                    RomSetId      = r.Id,
                    TotalMachines = r.Machines.Count,
                    CompleteMachines =
                        r.Machines.Count(m => m.Files.Count > 0 && m.Disks.Count == 0 &&
                                              m.Files.All(f => f.File.IsInRepo)) +
                        r.Machines.Count(m => m.Disks.Count > 0 && m.Files.Count == 0 &&
                                              m.Disks.All(f => f.Disk.IsInRepo)) +
                        r.Machines.Count(m => m.Files.Count > 0                 && m.Disks.Count > 0 &&
                                              m.Files.All(f => f.File.IsInRepo) && m.Disks.All(f => f.Disk.IsInRepo)),
                    IncompleteMachines =
                        r.Machines.Count(m => m.Files.Count > 0 && m.Disks.Count == 0 &&
                                              m.Files.Any(f => !f.File.IsInRepo)) +
                        r.Machines.Count(m => m.Disks.Count > 0 && m.Files.Count == 0 &&
                                              m.Disks.Any(f => !f.Disk.IsInRepo)) +
                        r.Machines.Count(m => m.Files.Count > 0 && m.Disks.Count > 0 &&
                                              (m.Files.Any(f => !f.File.IsInRepo) ||
                                               m.Disks.Any(f => !f.Disk.IsInRepo))),
                    TotalRoms = r.Machines.Sum(m => m.Files.Count) + r.Machines.Sum(m => m.Disks.Count) +
                                r.Machines.Sum(m => m.Medias.Count),
                    HaveRoms = r.Machines.Sum(m => m.Files.Count(f => f.File.IsInRepo)) +
                               r.Machines.Sum(m => m.Disks.Count(f => f.Disk.IsInRepo)) +
                               r.Machines.Sum(m => m.Medias.Count(f => f.Media.IsInRepo)),
                    MissRoms = r.Machines.Sum(m => m.Files.Count(f => !f.File.IsInRepo)) +
                               r.Machines.Sum(m => m.Disks.Count(f => !f.Disk.IsInRepo)) +
                               r.Machines.Sum(m => m.Medias.Count(f => !f.Media.IsInRepo))
                }).FirstOrDefault();

                RomSetStat oldStats = ctx.RomSetStats.Find(stats.RomSetId);

                if(oldStats != null)
                    ctx.Remove(oldStats);

                ctx.RomSetStats.Add(stats);

                ctx.SaveChanges();

                WorkFinished?.Invoke(this, new MessageEventArgs
                {
                    Message = string.Format(Localization.DatImportSuccess, stats.TotalMachines, stats.TotalRoms)
                });

                RomSetAdded?.Invoke(this, new RomSetEventArgs
                {
                    RomSet = new RomSetModel
                    {
                        Id                 = romSet.Id,
                        Author             = romSet.Author,
                        Comment            = romSet.Comment,
                        Date               = romSet.Date,
                        Description        = romSet.Description,
                        Filename           = romSet.Filename,
                        Homepage           = romSet.Homepage,
                        Name               = romSet.Name,
                        Sha384             = romSet.Sha384,
                        Version            = romSet.Version,
                        TotalMachines      = stats.TotalMachines,
                        CompleteMachines   = stats.CompleteMachines,
                        IncompleteMachines = stats.IncompleteMachines,
                        TotalRoms          = stats.TotalRoms,
                        HaveRoms           = stats.HaveRoms,
                        MissRoms           = stats.MissRoms,
                        Category           = romSet.Category
                    }
                });
            }
            catch(Exception e)
            {
                if(Debugger.IsAttached)
                    throw;

                ErrorOccurred?.Invoke(this, new ErrorEventArgs
                {
                    Message = Localization.UnhandledException
                });
            }
        }

        // TODO: Cancel and get back
        public void Abort() => _aborted = true;

        public event EventHandler                          SetIndeterminateProgress;
        public event EventHandler<MessageEventArgs>        WorkFinished;
        public event EventHandler<ErrorEventArgs>          ErrorOccurred;
        public event EventHandler<ProgressBoundsEventArgs> SetProgressBounds;
        public event EventHandler<ProgressEventArgs>       SetProgress;
        public event EventHandler<MessageEventArgs>        SetMessage;
        public event EventHandler<RomSetEventArgs>         RomSetAdded;
    }
}