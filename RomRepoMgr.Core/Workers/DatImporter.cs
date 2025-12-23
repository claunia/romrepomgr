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
// Copyright © 2020-2026 Natalia Portillo
*******************************************************************************/

using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using EFCore.BulkExtensions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using RomRepoMgr.Core.Checksums;
using RomRepoMgr.Core.EventArgs;
using RomRepoMgr.Core.Models;
using RomRepoMgr.Core.Resources;
using RomRepoMgr.Database;
using RomRepoMgr.Database.Models;
using SabreTools.DatFiles;
using SabreTools.DatTools;
using SabreTools.Models.Metadata;
using DatItem = SabreTools.DatItems.DatItem;
using Disk = SabreTools.DatItems.Formats.Disk;
using ErrorEventArgs = RomRepoMgr.Core.EventArgs.ErrorEventArgs;
using File = System.IO.File;
using Machine = RomRepoMgr.Database.Models.Machine;
using Media = SabreTools.DatItems.Formats.Media;
using Rom = SabreTools.DatItems.Formats.Rom;

namespace RomRepoMgr.Core.Workers;

public sealed class DatImporter
{
    static readonly Lock           DbLock = new();
    readonly        string         _category;
    readonly        string         _datFilesPath;
    readonly        string         _datPath;
    readonly        ILoggerFactory _loggerFactory;
    bool                           _aborted;

    public DatImporter(string datPath, string category, ILoggerFactory loggerFactory)
    {
        _datPath       = datPath;
        _datFilesPath  = Path.Combine(Settings.Settings.Current.RepositoryPath, "datfiles");
        _loggerFactory = loggerFactory;

        if(!string.IsNullOrWhiteSpace(category)) _category = category;
    }

    public void Import()
    {
        try
        {
            using var ctx = Context.Create(Settings.Settings.Current.DatabasePath, _loggerFactory);

            SetIndeterminateProgress?.Invoke(this, System.EventArgs.Empty);

            SetMessage?.Invoke(this,
                               new MessageEventArgs
                               {
                                   Message = Localization.ParsinDatFile
                               });

            DatFile datFile = Parser.ParseStatistics(_datPath);
            Parser.ParseInto(datFile, _datPath, throwOnError: true);

            SetMessage?.Invoke(this,
                               new MessageEventArgs
                               {
                                   Message = Localization.HashingDatFile
                               });

            string datHash = Sha384Context.File(_datPath, out byte[] datHashBinary);

            string datHash32 = Base32.ToBase32String(datHashBinary);

            if(!Directory.Exists(_datFilesPath)) Directory.CreateDirectory(_datFilesPath);

            string compressedDatPath = Path.Combine(_datFilesPath, datHash32 + ".lz");

            if(File.Exists(compressedDatPath))
            {
                ErrorOccurred?.Invoke(this,
                                      new ErrorEventArgs
                                      {
                                          Message = Localization.DatAlreadyInDatabase
                                      });

                return;
            }

            SetMessage?.Invoke(this,
                               new MessageEventArgs
                               {
                                   Message = Localization.AddingDatToDatabase
                               });

            // TODO: Check if there is a hash in database but not in repo

            var romSet = new RomSet
            {
                Author      = datFile.Header.GetStringFieldValue(Header.AuthorKey),
                Comment     = datFile.Header.GetStringFieldValue(Header.CommentKey),
                Date        = datFile.Header.GetStringFieldValue(Header.DateKey),
                Description = datFile.Header.GetStringFieldValue(Header.DescriptionKey),
                Filename    = Path.GetFileName(_datPath),
                Homepage    = datFile.Header.GetStringFieldValue(Header.HomepageKey),
                Name        = datFile.Header.GetStringFieldValue(Header.NameKey),
                Sha384      = datHash,
                Version     = datFile.Header.GetStringFieldValue(Header.VersionKey),
                CreatedOn   = DateTime.UtcNow,
                UpdatedOn   = DateTime.UtcNow,
                Category    = _category
            };

            lock(DbLock)
            {
                ctx.RomSets.Add(romSet);
                ctx.SaveChanges();
            }

            SetMessage?.Invoke(this,
                               new MessageEventArgs
                               {
                                   Message = Localization.CompressingDatFile
                               });

            var datCompress = new Compression();
            datCompress.SetProgress       += SetProgress;
            datCompress.SetProgressBounds += SetProgressBounds;
            datCompress.CompressFile(_datPath, compressedDatPath);

            SetMessage?.Invoke(this,
                               new MessageEventArgs
                               {
                                   Message = Localization.GettingMachineNames
                               });

            var machineNames = datFile.Items.SortedKeys.Distinct().ToList();

            SetMessage?.Invoke(this,
                               new MessageEventArgs
                               {
                                   Message = Localization.AddingMachines
                               });

            SetProgressBounds?.Invoke(this,
                                      new ProgressBoundsEventArgs
                                      {
                                          Minimum = 0,
                                          Maximum = machineNames.Count
                                      });

            int position = 0;
            var machines = new Dictionary<string, Machine>();

            foreach(string name in machineNames)
            {
                SetProgress?.Invoke(this,
                                    new ProgressEventArgs
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

            SetMessage?.Invoke(this,
                               new MessageEventArgs
                               {
                                   Message = Localization.SavingChangesToDatabase
                               });

            SetIndeterminateProgress?.Invoke(this, System.EventArgs.Empty);

            lock(DbLock)
            {
                ctx.BulkInsert(machines.Values.ToList(), b => b.SetOutputIdentity = true);
                ctx.SaveChanges();
            }

            SetMessage?.Invoke(this,
                               new MessageEventArgs
                               {
                                   Message = Localization.RetrievingRomsAndDisks
                               });

            var roms   = new List<Rom>();
            var disks  = new List<Disk>();
            var medias = new List<Media>();

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

            SetProgressBounds?.Invoke(this,
                                      new ProgressBoundsEventArgs
                                      {
                                          Minimum = 0,
                                          Maximum = datFile.Items.SortedKeys.Length
                                      });

            List<DbFile>                pendingFilesByCrcList;
            List<DbFile>                pendingFilesByMd5List;
            List<DbFile>                pendingFilesBySha1List;
            List<DbFile>                pendingFilesBySha256List;
            List<DbFile>                pendingFilesBySha384List;
            List<DbFile>                pendingFilesBySha512List;
            Dictionary<string, DbDisk>  pendingDisksByMd5;
            Dictionary<string, DbDisk>  pendingDisksBySha1;
            Dictionary<string, DbMedia> pendingMediasByMd5;
            Dictionary<string, DbMedia> pendingMediasBySha1;
            Dictionary<string, DbMedia> pendingMediasBySha256;

            lock(DbLock)
            {
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

                    foreach(string key in datFile.Items.SortedKeys)
                    {
                        SetProgress?.Invoke(this,
                                            new ProgressEventArgs
                                            {
                                                Value = position
                                            });

                        foreach(DatItem item in datFile.GetItemsForBucket(key))
                        {
                            switch(item)
                            {
                                case Rom rom:
                                    if(rom.GetStringFieldValue(SabreTools.Models.Metadata.Rom.CRCKey) != null)
                                    {
                                        dbcc = dbConnection.CreateCommand();

                                        dbcc.CommandText =
                                            $"INSERT INTO \"{tmpRomCrc32Table}\" (\"Size\", \"Crc32\") VALUES (\"{(ulong)rom.GetInt64FieldValue(SabreTools.Models.Metadata.Rom.SizeKey)}\", \"{rom.GetStringFieldValue(SabreTools.Models.Metadata.Rom.CRCKey)}\");";

                                        dbcc.ExecuteNonQuery();

                                        romsHaveCrc = true;
                                    }

                                    if(rom.GetStringFieldValue(SabreTools.Models.Metadata.Rom.MD5Key) != null)
                                    {
                                        dbcc = dbConnection.CreateCommand();

                                        dbcc.CommandText =
                                            $"INSERT INTO \"{tmpRomMd5Table}\" (\"Size\", \"Md5\") VALUES (\"{(ulong)rom.GetInt64FieldValue(SabreTools.Models.Metadata.Rom.SizeKey)}\", \"{rom.GetStringFieldValue(SabreTools.Models.Metadata.Rom.MD5Key)}\");";

                                        dbcc.ExecuteNonQuery();

                                        romsHaveMd5 = true;
                                    }

                                    if(rom.GetStringFieldValue(SabreTools.Models.Metadata.Rom.SHA1Key) != null)
                                    {
                                        dbcc = dbConnection.CreateCommand();

                                        dbcc.CommandText =
                                            $"INSERT INTO \"{tmpRomSha1Table}\" (\"Size\", \"Sha1\") VALUES (\"{(ulong)rom.GetInt64FieldValue(SabreTools.Models.Metadata.Rom.SizeKey)}\", \"{rom.GetStringFieldValue(SabreTools.Models.Metadata.Rom.SHA1Key)}\");";

                                        dbcc.ExecuteNonQuery();

                                        romsHaveSha1 = true;
                                    }

                                    if(rom.GetStringFieldValue(SabreTools.Models.Metadata.Rom.SHA256Key) != null)
                                    {
                                        dbcc = dbConnection.CreateCommand();

                                        dbcc.CommandText =
                                            $"INSERT INTO \"{tmpRomSha256Table}\" (\"Size\", \"Sha256\") VALUES (\"{(ulong)rom.GetInt64FieldValue(SabreTools.Models.Metadata.Rom.SizeKey)}\", \"{rom.GetStringFieldValue(SabreTools.Models.Metadata.Rom.SHA256Key)}\");";

                                        dbcc.ExecuteNonQuery();

                                        romsHaveSha256 = true;
                                    }

                                    if(rom.GetStringFieldValue(SabreTools.Models.Metadata.Rom.SHA384Key) != null)
                                    {
                                        dbcc = dbConnection.CreateCommand();

                                        dbcc.CommandText =
                                            $"INSERT INTO \"{tmpRomSha384Table}\" (\"Size\", \"Sha384\") VALUES (\"{(ulong)rom.GetInt64FieldValue(SabreTools.Models.Metadata.Rom.SizeKey)}\", \"{rom.GetStringFieldValue(SabreTools.Models.Metadata.Rom.SHA384Key)}\");";

                                        dbcc.ExecuteNonQuery();

                                        romsHaveSha384 = true;
                                    }

                                    if(rom.GetStringFieldValue(SabreTools.Models.Metadata.Rom.SHA512Key) != null)
                                    {
                                        dbcc = dbConnection.CreateCommand();

                                        dbcc.CommandText =
                                            $"INSERT INTO \"{tmpRomSha512Table}\" (\"Size\", \"Sha512\") VALUES (\"{(ulong)rom.GetInt64FieldValue(SabreTools.Models.Metadata.Rom.SizeKey)}\", \"{rom.GetStringFieldValue(SabreTools.Models.Metadata.Rom.SHA512Key)}\");";

                                        dbcc.ExecuteNonQuery();

                                        romsHaveSha512 = true;
                                    }

                                    roms.Add(rom);

                                    continue;
                                case Disk disk:
                                    if(disk.GetStringFieldValue(SabreTools.Models.Metadata.Disk.MD5Key) != null)
                                    {
                                        dbcc = dbConnection.CreateCommand();

                                        dbcc.CommandText =
                                            $"INSERT INTO \"{tmpDiskMd5Table}\" (\"Md5\") VALUES (\"{disk.GetStringFieldValue(SabreTools.Models.Metadata.Disk.MD5Key)}\");";

                                        dbcc.ExecuteNonQuery();

                                        disksHaveMd5 = true;
                                    }

                                    if(disk.GetStringFieldValue(SabreTools.Models.Metadata.Disk.SHA1Key) != null)
                                    {
                                        dbcc = dbConnection.CreateCommand();

                                        dbcc.CommandText =
                                            $"INSERT INTO \"{tmpDiskSha1Table}\" (\"Sha1\") VALUES (\"{disk.GetStringFieldValue(SabreTools.Models.Metadata.Disk.SHA1Key)}\");";

                                        dbcc.ExecuteNonQuery();

                                        disksHaveSha1 = true;
                                    }

                                    disks.Add(disk);

                                    continue;
                                case Media media:
                                    if(media.GetStringFieldValue(SabreTools.Models.Metadata.Media.MD5Key) != null)
                                    {
                                        dbcc = dbConnection.CreateCommand();

                                        dbcc.CommandText =
                                            $"INSERT INTO \"{tmpMediaMd5Table}\" (\"Md5\") VALUES (\"{media.GetStringFieldValue(SabreTools.Models.Metadata.Media.MD5Key)}\");";

                                        dbcc.ExecuteNonQuery();

                                        mediasHaveMd5 = true;
                                    }

                                    if(media.GetStringFieldValue(SabreTools.Models.Metadata.Media.SHA1Key) != null)
                                    {
                                        dbcc = dbConnection.CreateCommand();

                                        dbcc.CommandText =
                                            $"INSERT INTO \"{tmpMediaSha1Table}\" (\"Sha1\") VALUES (\"{media.GetStringFieldValue(SabreTools.Models.Metadata.Media.SHA1Key)}\");";

                                        dbcc.ExecuteNonQuery();

                                        mediasHaveSha1 = true;
                                    }

                                    if(media.GetStringFieldValue(SabreTools.Models.Metadata.Media.SHA256Key) != null)
                                    {
                                        dbcc = dbConnection.CreateCommand();

                                        dbcc.CommandText =
                                            $"INSERT INTO \"{tmpMediaSha256Table}\" (\"Sha256\") VALUES (\"{media.GetStringFieldValue(SabreTools.Models.Metadata.Media.SHA256Key)}\");";

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

                pendingFilesByCrcList = romsHaveCrc
                                            ? ctx.Files
                                                 .FromSqlRaw($"SELECT DISTINCT f.* FROM Files AS f, [{tmpRomCrc32Table}] AS t WHERE f.Crc32 = t.Crc32 AND f.Size = t.Size")
                                                 .ToList()
                                            : [];

                pendingFilesByMd5List = romsHaveMd5
                                            ? ctx.Files
                                                 .FromSqlRaw($"SELECT DISTINCT f.* FROM Files AS f, [{tmpRomMd5Table}] AS t WHERE f.Md5 = t.Md5 AND f.Size = t.Size")
                                                 .ToList()
                                            : [];

                pendingFilesBySha1List = romsHaveSha1
                                             ? ctx.Files
                                                  .FromSqlRaw($"SELECT DISTINCT f.* FROM Files AS f, [{tmpRomSha1Table}] AS t WHERE f.Sha1 = t.Sha1 AND f.Size = t.Size")
                                                  .ToList()
                                             : [];

                pendingFilesBySha256List = romsHaveSha256
                                               ? ctx.Files
                                                    .FromSqlRaw($"SELECT DISTINCT f.* FROM Files AS f, [{tmpRomSha256Table}] AS t WHERE f.Sha256 = t.Sha256 AND f.Size = t.Size")
                                                    .ToList()
                                               : [];

                pendingFilesBySha384List = romsHaveSha384
                                               ? ctx.Files
                                                    .FromSqlRaw($"SELECT DISTINCT f.* FROM Files AS f, [{tmpRomSha384Table}] AS t WHERE f.Sha384 = t.Sha384 AND f.Size = t.Size")
                                                    .ToList()
                                               : [];

                pendingFilesBySha512List = romsHaveSha512
                                               ? ctx.Files
                                                    .FromSqlRaw($"SELECT DISTINCT f.* FROM Files AS f, [{tmpRomSha512Table}] AS t WHERE f.Sha512 = t.Sha512 AND f.Size = t.Size")
                                                    .ToList()
                                               : [];

                pendingDisksByMd5 = disksHaveMd5
                                        ? ctx.Disks
                                             .FromSqlRaw($"SELECT DISTINCT f.* FROM Disks AS f, [{tmpDiskMd5Table}] AS t WHERE f.Md5 = t.Md5")
                                             .ToDictionary(f => f.Md5)
                                        : [];

                pendingDisksBySha1 = disksHaveSha1
                                         ? ctx.Disks
                                              .FromSqlRaw($"SELECT DISTINCT f.* FROM Disks AS f, [{tmpDiskSha1Table}] AS t WHERE f.Sha1 = t.Sha1")
                                              .ToDictionary(f => f.Sha1)
                                         : [];

                pendingMediasByMd5 = mediasHaveMd5
                                         ? ctx.Medias
                                              .FromSqlRaw($"SELECT DISTINCT f.* FROM Medias AS f, [{tmpMediaMd5Table}] AS t WHERE f.Md5 = t.Md5")
                                              .ToDictionary(f => f.Md5)
                                         : [];

                pendingMediasBySha1 = mediasHaveSha1
                                          ? ctx.Medias
                                               .FromSqlRaw($"SELECT DISTINCT f.* FROM Medias AS f, [{tmpMediaSha1Table}] AS t WHERE f.Sha1 = t.Sha1")
                                               .ToDictionary(f => f.Sha1)
                                          : [];

                pendingMediasBySha256 = mediasHaveSha256
                                            ? ctx.Medias
                                                 .FromSqlRaw($"SELECT DISTINCT f.* FROM Medias AS f, [{tmpMediaSha256Table}] AS t WHERE f.Sha256 = t.Sha256")
                                                 .ToDictionary(f => f.Sha256)
                                            : [];
            }

            var pendingFilesByCrc    = new Dictionary<string, DbFile>();
            var pendingFilesByMd5    = new Dictionary<string, DbFile>();
            var pendingFilesBySha1   = new Dictionary<string, DbFile>();
            var pendingFilesBySha256 = new Dictionary<string, DbFile>();
            var pendingFilesBySha384 = new Dictionary<string, DbFile>();
            var pendingFilesBySha512 = new Dictionary<string, DbFile>();

            var pendingFiles = pendingFilesByCrcList.Where(item => !pendingFilesByCrc.TryAdd(item.Crc32, item))
                                                    .ToList();

            pendingFiles.AddRange(pendingFilesByMd5List.Where(item => !pendingFilesByMd5.TryAdd(item.Md5, item)));
            pendingFiles.AddRange(pendingFilesBySha1List.Where(item => !pendingFilesBySha1.TryAdd(item.Sha1, item)));

            pendingFiles.AddRange(pendingFilesBySha256List.Where(item => !pendingFilesBySha256
                                                                            .TryAdd(item.Sha256, item)));

            pendingFiles.AddRange(pendingFilesBySha384List.Where(item => !pendingFilesBySha384
                                                                            .TryAdd(item.Sha384, item)));

            pendingFiles.AddRange(pendingFilesBySha512List.Where(item => !pendingFilesBySha512
                                                                            .TryAdd(item.Sha512, item)));

            // Clear some memory
            pendingFilesByCrcList.Clear();
            pendingFilesByMd5List.Clear();
            pendingFilesBySha1List.Clear();
            pendingFilesBySha256List.Clear();
            pendingFilesBySha384List.Clear();
            pendingFilesBySha512List.Clear();

            lock(DbLock)
            {
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
            }

            SetProgressBounds?.Invoke(this,
                                      new ProgressBoundsEventArgs
                                      {
                                          Minimum = 0,
                                          Maximum = roms.Count
                                      });

            SetMessage?.Invoke(this,
                               new MessageEventArgs
                               {
                                   Message = Localization.AddingRoms
                               });

            position = 0;

            var newFiles          = new List<DbFile>();
            var newFilesByMachine = new List<FileByMachine>();

            foreach(Rom rom in roms)
            {
                bool hashCollision = false;

                SetProgress?.Invoke(this,
                                    new ProgressEventArgs
                                    {
                                        Value = position
                                    });

                if(!machines.TryGetValue(rom.GetFieldValue<SabreTools.DatItems.Machine>(DatItem.MachineKey)
                                           ?.GetStringFieldValue(SabreTools.Models.Metadata.Machine.NameKey)
                                           ?.ToLowerInvariant(),
                                         out Machine machine))
                {
                    ErrorOccurred?.Invoke(this,
                                          new ErrorEventArgs
                                          {
                                              Message = Localization.FoundRomWithoutMachine
                                          });

                    return;
                }

                ulong uSize = (ulong)rom.GetInt64FieldValue(SabreTools.Models.Metadata.Rom.SizeKey);

                DbFile file = null;

                if(rom.GetStringFieldValue(SabreTools.Models.Metadata.Rom.SHA512Key) != null)
                {
                    if(pendingFilesBySha512.TryGetValue(rom.GetStringFieldValue(SabreTools.Models.Metadata.Rom
                                                                                   .SHA512Key),
                                                        out file))
                    {
                        if(file.Size != uSize)
                        {
                            hashCollision = true;
                            file          = null;
                        }
                    }
                }

                if(rom.GetStringFieldValue(SabreTools.Models.Metadata.Rom.SHA384Key) != null && file == null)
                {
                    if(pendingFilesBySha384.TryGetValue(rom.GetStringFieldValue(SabreTools.Models.Metadata.Rom
                                                                                   .SHA384Key),
                                                        out file))
                    {
                        if(file.Size != uSize)
                        {
                            hashCollision = true;
                            file          = null;
                        }
                    }
                }

                if(rom.GetStringFieldValue(SabreTools.Models.Metadata.Rom.SHA256Key) != null && file == null)
                {
                    if(pendingFilesBySha256.TryGetValue(rom.GetStringFieldValue(SabreTools.Models.Metadata.Rom
                                                                                   .SHA256Key),
                                                        out file))
                    {
                        if(file.Size != uSize)
                        {
                            hashCollision = true;
                            file          = null;
                        }
                    }
                }

                if(rom.GetStringFieldValue(SabreTools.Models.Metadata.Rom.SHA1Key) != null && file == null)
                {
                    if(pendingFilesBySha1.TryGetValue(rom.GetStringFieldValue(SabreTools.Models.Metadata.Rom.SHA1Key),
                                                      out file))
                    {
                        if(file.Size != uSize)
                        {
                            hashCollision = true;
                            file          = null;
                        }
                    }
                }

                if(rom.GetStringFieldValue(SabreTools.Models.Metadata.Rom.MD5Key) != null && file == null)
                {
                    if(pendingFilesByMd5.TryGetValue(rom.GetStringFieldValue(SabreTools.Models.Metadata.Rom.MD5Key),
                                                     out file))
                    {
                        if(file.Size != uSize)
                        {
                            hashCollision = true;
                            file          = null;
                        }
                    }
                }

                if(rom.GetStringFieldValue(SabreTools.Models.Metadata.Rom.CRCKey) != null && file == null)
                {
                    if(pendingFilesByCrc.TryGetValue(rom.GetStringFieldValue(SabreTools.Models.Metadata.Rom.CRCKey),
                                                     out file))
                    {
                        if(file.Size != uSize)
                        {
                            hashCollision = true;
                            file          = null;
                        }
                    }
                }

                if(file == null && hashCollision)
                {
                    if(rom.GetStringFieldValue(SabreTools.Models.Metadata.Rom.SHA512Key) != null)
                    {
                        file = pendingFiles.Find(f => f.Sha512 ==
                                                      rom.GetStringFieldValue(SabreTools.Models.Metadata.Rom
                                                                                 .SHA512Key) &&
                                                      f.Size == uSize);
                    }

                    if(file == null && rom.GetStringFieldValue(SabreTools.Models.Metadata.Rom.SHA384Key) != null)
                    {
                        file = pendingFiles.Find(f => f.Sha384 ==
                                                      rom.GetStringFieldValue(SabreTools.Models.Metadata.Rom
                                                                                 .SHA384Key) &&
                                                      f.Size == uSize);
                    }

                    if(file == null && rom.GetStringFieldValue(SabreTools.Models.Metadata.Rom.SHA256Key) != null)
                    {
                        file = pendingFiles.Find(f => f.Sha256 ==
                                                      rom.GetStringFieldValue(SabreTools.Models.Metadata.Rom
                                                                                 .SHA256Key) &&
                                                      f.Size == uSize);
                    }

                    if(file == null && rom.GetStringFieldValue(SabreTools.Models.Metadata.Rom.SHA1Key) != null)
                    {
                        file = pendingFiles.Find(f => f.Sha1 ==
                                                      rom.GetStringFieldValue(SabreTools.Models.Metadata.Rom.SHA1Key) &&
                                                      f.Size == uSize);
                    }

                    if(file == null && rom.GetStringFieldValue(SabreTools.Models.Metadata.Rom.MD5Key) != null)
                    {
                        file = pendingFiles.Find(f => f.Md5 ==
                                                      rom.GetStringFieldValue(SabreTools.Models.Metadata.Rom.MD5Key) &&
                                                      f.Size == uSize);
                    }

                    if(file == null && rom.GetStringFieldValue(SabreTools.Models.Metadata.Rom.CRCKey) != null)
                    {
                        file = pendingFiles.Find(f => f.Crc32 ==
                                                      rom.GetStringFieldValue(SabreTools.Models.Metadata.Rom.CRCKey) &&
                                                      f.Size == uSize);
                    }
                }

                if(file == null)
                {
                    file = new DbFile
                    {
                        Crc32     = rom.GetStringFieldValue(SabreTools.Models.Metadata.Rom.CRCKey),
                        CreatedOn = DateTime.UtcNow,
                        Md5       = rom.GetStringFieldValue(SabreTools.Models.Metadata.Rom.MD5Key),
                        Sha1      = rom.GetStringFieldValue(SabreTools.Models.Metadata.Rom.SHA1Key),
                        Sha256    = rom.GetStringFieldValue(SabreTools.Models.Metadata.Rom.SHA256Key),
                        Sha384    = rom.GetStringFieldValue(SabreTools.Models.Metadata.Rom.SHA384Key),
                        Sha512    = rom.GetStringFieldValue(SabreTools.Models.Metadata.Rom.SHA512Key),
                        Size      = uSize,
                        UpdatedOn = DateTime.UtcNow
                    };

                    newFiles.Add(file);
                }

                if(string.IsNullOrEmpty(file.Crc32) &&
                   !string.IsNullOrEmpty(rom.GetStringFieldValue(SabreTools.Models.Metadata.Rom.CRCKey)))
                {
                    file.Crc32     = rom.GetStringFieldValue(SabreTools.Models.Metadata.Rom.CRCKey);
                    file.UpdatedOn = DateTime.UtcNow;
                }

                if(string.IsNullOrEmpty(file.Md5) &&
                   !string.IsNullOrEmpty(rom.GetStringFieldValue(SabreTools.Models.Metadata.Rom.MD5Key)))
                {
                    file.Md5       = rom.GetStringFieldValue(SabreTools.Models.Metadata.Rom.MD5Key);
                    file.UpdatedOn = DateTime.UtcNow;
                }

                if(string.IsNullOrEmpty(file.Sha1) &&
                   !string.IsNullOrEmpty(rom.GetStringFieldValue(SabreTools.Models.Metadata.Rom.SHA1Key)))
                {
                    file.Sha1      = rom.GetStringFieldValue(SabreTools.Models.Metadata.Rom.SHA1Key);
                    file.UpdatedOn = DateTime.UtcNow;
                }

                if(string.IsNullOrEmpty(file.Sha256) &&
                   !string.IsNullOrEmpty(rom.GetStringFieldValue(SabreTools.Models.Metadata.Rom.SHA256Key)))
                {
                    file.Sha256    = rom.GetStringFieldValue(SabreTools.Models.Metadata.Rom.SHA256Key);
                    file.UpdatedOn = DateTime.UtcNow;
                }

                if(string.IsNullOrEmpty(file.Sha384) &&
                   !string.IsNullOrEmpty(rom.GetStringFieldValue(SabreTools.Models.Metadata.Rom.SHA384Key)))
                {
                    file.Sha384    = rom.GetStringFieldValue(SabreTools.Models.Metadata.Rom.SHA384Key);
                    file.UpdatedOn = DateTime.UtcNow;
                }

                if(string.IsNullOrEmpty(file.Sha512) &&
                   !string.IsNullOrEmpty(rom.GetStringFieldValue(SabreTools.Models.Metadata.Rom.SHA512Key)))
                {
                    file.Sha512    = rom.GetStringFieldValue(SabreTools.Models.Metadata.Rom.SHA512Key);
                    file.UpdatedOn = DateTime.UtcNow;
                }

                DateTime? fileModificationDate = null;

                if(!string.IsNullOrEmpty(rom.GetStringFieldValue(SabreTools.Models.Metadata.Rom.DateKey)))
                {
                    string romDate = rom.GetStringFieldValue(SabreTools.Models.Metadata.Rom.DateKey).Replace("/", "\\");

                    if(DateTime.TryParseExact(romDate,
                                              @"yyyy\\M\\d H:mm",
                                              CultureInfo.InvariantCulture,
                                              DateTimeStyles.AssumeUniversal,
                                              out DateTime date))
                        fileModificationDate = date;
                }

                string filename;
                string path = null;

                if(rom.GetStringFieldValue(SabreTools.Models.Metadata.Rom.NameKey).Contains('\\'))
                {
                    filename = Path.GetFileName(rom.GetStringFieldValue(SabreTools.Models.Metadata.Rom.NameKey)
                                                   .Replace('\\', '/'));

                    path = Path.GetDirectoryName(rom.GetStringFieldValue(SabreTools.Models.Metadata.Rom.NameKey)
                                                    .Replace('\\', '/'));
                }
                else if(rom.GetStringFieldValue(SabreTools.Models.Metadata.Rom.NameKey).Contains('/'))
                {
                    filename = Path.GetFileName(rom.GetStringFieldValue(SabreTools.Models.Metadata.Rom.NameKey));
                    path     = Path.GetDirectoryName(rom.GetStringFieldValue(SabreTools.Models.Metadata.Rom.NameKey));
                }
                else
                    filename = rom.GetStringFieldValue(SabreTools.Models.Metadata.Rom.NameKey);

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
                    pendingFilesByMd5[file.Md5]                           = file;
                else if(file.Crc32 != null) pendingFilesByCrc[file.Crc32] = file;

                position++;
            }

            SetMessage?.Invoke(this,
                               new MessageEventArgs
                               {
                                   Message = Localization.SavingChangesToDatabase
                               });

            SetIndeterminateProgress?.Invoke(this, System.EventArgs.Empty);

            lock(DbLock)
            {
                ctx.BulkInsert(newFiles, b => b.SetOutputIdentity = true);
            }

            foreach(FileByMachine fbm in newFilesByMachine)
            {
                fbm.FileId    = fbm.File.Id;
                fbm.MachineId = fbm.Machine.Id;
            }

            lock(DbLock)
            {
                ctx.BulkInsert(newFilesByMachine);

                ctx.SaveChanges();
            }

            pendingFilesBySha512.Clear();
            pendingFilesBySha384.Clear();
            pendingFilesBySha256.Clear();
            pendingFilesBySha1.Clear();
            pendingFilesByMd5.Clear();
            pendingFilesByCrc.Clear();
            pendingFiles.Clear();
            newFiles.Clear();
            newFilesByMachine.Clear();

            SetProgressBounds?.Invoke(this,
                                      new ProgressBoundsEventArgs
                                      {
                                          Minimum = 0,
                                          Maximum = disks.Count
                                      });

            SetMessage?.Invoke(this,
                               new MessageEventArgs
                               {
                                   Message = Localization.AddingDisks
                               });

            position = 0;

            var newDisks          = new List<DbDisk>();
            var newDisksByMachine = new List<DiskByMachine>();

            foreach(Disk disk in disks)
            {
                SetProgress?.Invoke(this,
                                    new ProgressEventArgs
                                    {
                                        Value = position
                                    });

                if(!machines.TryGetValue(disk.GetFieldValue<SabreTools.DatItems.Machine>(DatItem.MachineKey)
                                            ?.GetStringFieldValue(SabreTools.Models.Metadata.Machine.NameKey)
                                            ?.ToLowerInvariant(),
                                         out Machine machine))
                {
                    ErrorOccurred?.Invoke(this,
                                          new ErrorEventArgs
                                          {
                                              Message = Localization.FoundDiskWithoutMachine
                                          });

                    return;
                }

                if(disk.GetStringFieldValue(SabreTools.Models.Metadata.Disk.MD5Key)  == null &&
                   disk.GetStringFieldValue(SabreTools.Models.Metadata.Disk.SHA1Key) == null)
                {
                    position++;

                    continue;
                }

                DbDisk dbDisk = null;

                if(disk.GetStringFieldValue(SabreTools.Models.Metadata.Disk.SHA1Key) != null && dbDisk == null)
                {
                    pendingDisksBySha1.TryGetValue(disk.GetStringFieldValue(SabreTools.Models.Metadata.Disk.SHA1Key),
                                                   out dbDisk);
                }

                if(disk.GetStringFieldValue(SabreTools.Models.Metadata.Disk.MD5Key) != null && dbDisk == null)
                {
                    pendingDisksByMd5.TryGetValue(disk.GetStringFieldValue(SabreTools.Models.Metadata.Disk.MD5Key),
                                                  out dbDisk);
                }

                if(dbDisk == null)
                {
                    dbDisk = new DbDisk
                    {
                        CreatedOn = DateTime.UtcNow,
                        Md5       = disk.GetStringFieldValue(SabreTools.Models.Metadata.Disk.MD5Key),
                        Sha1      = disk.GetStringFieldValue(SabreTools.Models.Metadata.Disk.SHA1Key),
                        UpdatedOn = DateTime.UtcNow
                    };

                    newDisks.Add(dbDisk);
                }

                if(string.IsNullOrEmpty(dbDisk.Md5) &&
                   !string.IsNullOrEmpty(disk.GetStringFieldValue(SabreTools.Models.Metadata.Disk.MD5Key)))
                {
                    dbDisk.Md5       = disk.GetStringFieldValue(SabreTools.Models.Metadata.Disk.MD5Key);
                    dbDisk.UpdatedOn = DateTime.UtcNow;
                }

                if(string.IsNullOrEmpty(dbDisk.Sha1) &&
                   !string.IsNullOrEmpty(disk.GetStringFieldValue(SabreTools.Models.Metadata.Disk.SHA1Key)))
                {
                    dbDisk.Sha1      = disk.GetStringFieldValue(SabreTools.Models.Metadata.Disk.SHA1Key);
                    dbDisk.UpdatedOn = DateTime.UtcNow;
                }

                newDisksByMachine.Add(new DiskByMachine
                {
                    Disk    = dbDisk,
                    Machine = machine,
                    Name    = disk.GetStringFieldValue(SabreTools.Models.Metadata.Media.NameKey)
                });

                if(dbDisk.Sha1 != null) pendingDisksBySha1[dbDisk.Sha1] = dbDisk;

                if(dbDisk.Md5 != null) pendingDisksByMd5[dbDisk.Md5] = dbDisk;

                position++;
            }

            SetMessage?.Invoke(this,
                               new MessageEventArgs
                               {
                                   Message = Localization.SavingChangesToDatabase
                               });

            SetIndeterminateProgress?.Invoke(this, System.EventArgs.Empty);

            lock(DbLock)
            {
                ctx.BulkInsert(newDisks, b => b.SetOutputIdentity = true);
            }

            foreach(DiskByMachine dbm in newDisksByMachine)
            {
                dbm.DiskId    = dbm.Disk.Id;
                dbm.MachineId = dbm.Machine.Id;
            }

            lock(DbLock)
            {
                ctx.BulkInsert(newDisksByMachine);

                ctx.SaveChanges();
            }

            pendingDisksBySha1.Clear();
            pendingDisksByMd5.Clear();
            newDisks.Clear();
            newDisksByMachine.Clear();

            SetProgressBounds?.Invoke(this,
                                      new ProgressBoundsEventArgs
                                      {
                                          Minimum = 0,
                                          Maximum = medias.Count
                                      });

            SetMessage?.Invoke(this,
                               new MessageEventArgs
                               {
                                   Message = Localization.AddingMedias
                               });

            position = 0;

            var newMedias          = new List<DbMedia>();
            var newMediasByMachine = new List<MediaByMachine>();

            foreach(Media media in medias)
            {
                SetProgress?.Invoke(this,
                                    new ProgressEventArgs
                                    {
                                        Value = position
                                    });

                if(!machines.TryGetValue(media.GetFieldValue<SabreTools.DatItems.Machine>(DatItem.MachineKey)
                                             ?.GetStringFieldValue(SabreTools.Models.Metadata.Machine.NameKey)
                                             ?.ToLowerInvariant(),
                                         out Machine machine))
                {
                    ErrorOccurred?.Invoke(this,
                                          new ErrorEventArgs
                                          {
                                              Message = Localization.FoundMediaWithoutMachine
                                          });

                    return;
                }

                if(media.GetStringFieldValue(SabreTools.Models.Metadata.Media.MD5Key)    == null &&
                   media.GetStringFieldValue(SabreTools.Models.Metadata.Media.SHA1Key)   == null &&
                   media.GetStringFieldValue(SabreTools.Models.Metadata.Media.SHA256Key) == null)
                {
                    position++;

                    continue;
                }

                DbMedia dbMedia = null;

                if(media.GetStringFieldValue(SabreTools.Models.Metadata.Media.SHA256Key) != null && dbMedia == null)
                {
                    pendingMediasBySha256.TryGetValue(media.GetStringFieldValue(SabreTools.Models.Metadata.Media
                                                                                   .SHA256Key),
                                                      out dbMedia);
                }

                if(media.GetStringFieldValue(SabreTools.Models.Metadata.Media.SHA1Key) != null && dbMedia == null)
                {
                    pendingMediasBySha1.TryGetValue(media.GetStringFieldValue(SabreTools.Models.Metadata.Media.SHA1Key),
                                                    out dbMedia);
                }

                if(media.GetStringFieldValue(SabreTools.Models.Metadata.Media.MD5Key) != null && dbMedia == null)
                {
                    pendingMediasByMd5.TryGetValue(media.GetStringFieldValue(SabreTools.Models.Metadata.Media.MD5Key),
                                                   out dbMedia);
                }

                // TODO: SpamSum
                if(dbMedia == null)
                {
                    dbMedia = new DbMedia
                    {
                        CreatedOn = DateTime.UtcNow,
                        Md5       = media.GetStringFieldValue(SabreTools.Models.Metadata.Media.MD5Key),
                        Sha1      = media.GetStringFieldValue(SabreTools.Models.Metadata.Media.SHA1Key),
                        Sha256    = media.GetStringFieldValue(SabreTools.Models.Metadata.Media.SHA256Key),
                        UpdatedOn = DateTime.UtcNow
                    };

                    newMedias.Add(dbMedia);
                }

                if(string.IsNullOrEmpty(dbMedia.Md5) &&
                   !string.IsNullOrEmpty(media.GetStringFieldValue(SabreTools.Models.Metadata.Media.MD5Key)))
                {
                    dbMedia.Md5       = media.GetStringFieldValue(SabreTools.Models.Metadata.Media.MD5Key);
                    dbMedia.UpdatedOn = DateTime.UtcNow;
                }

                if(string.IsNullOrEmpty(dbMedia.Sha1) &&
                   !string.IsNullOrEmpty(media.GetStringFieldValue(SabreTools.Models.Metadata.Media.SHA1Key)))
                {
                    dbMedia.Sha1      = media.GetStringFieldValue(SabreTools.Models.Metadata.Media.SHA1Key);
                    dbMedia.UpdatedOn = DateTime.UtcNow;
                }

                if(string.IsNullOrEmpty(dbMedia.Sha256) &&
                   !string.IsNullOrEmpty(media.GetStringFieldValue(SabreTools.Models.Metadata.Media.SHA256Key)))
                {
                    dbMedia.Sha256    = media.GetStringFieldValue(SabreTools.Models.Metadata.Media.SHA256Key);
                    dbMedia.UpdatedOn = DateTime.UtcNow;
                }

                newMediasByMachine.Add(new MediaByMachine
                {
                    Media   = dbMedia,
                    Machine = machine,
                    Name    = media.GetStringFieldValue(SabreTools.Models.Metadata.Media.NameKey)
                });

                if(dbMedia.Sha256 != null) pendingMediasBySha256[dbMedia.Sha256] = dbMedia;

                if(dbMedia.Sha1 != null) pendingMediasBySha1[dbMedia.Sha1] = dbMedia;

                if(dbMedia.Md5 != null) pendingMediasByMd5[dbMedia.Md5] = dbMedia;

                position++;
            }

            SetMessage?.Invoke(this,
                               new MessageEventArgs
                               {
                                   Message = Localization.SavingChangesToDatabase
                               });

            SetIndeterminateProgress?.Invoke(this, System.EventArgs.Empty);

            lock(DbLock)
            {
                ctx.BulkInsert(newMedias, b => b.SetOutputIdentity = true);
            }

            foreach(MediaByMachine mbm in newMediasByMachine)
            {
                mbm.MediaId   = mbm.Media.Id;
                mbm.MachineId = mbm.Machine.Id;
            }

            lock(DbLock)
            {
                ctx.BulkInsert(newMediasByMachine);

                ctx.SaveChanges();
            }

            pendingMediasBySha256.Clear();
            pendingMediasBySha1.Clear();
            pendingMediasByMd5.Clear();
            newMedias.Clear();
            newMediasByMachine.Clear();
            RomSetStat stats;

            lock(DbLock)
            {
                stats = ctx.RomSets.Where(r => r.Id == romSet.Id)
                           .Select(r => new RomSetStat
                            {
                                RomSetId      = r.Id,
                                TotalMachines = r.Machines.Count,
                                CompleteMachines =
                                    r.Machines.Count(m => m.Files.Count > 0  &&
                                                          m.Disks.Count == 0 &&
                                                          m.Files.All(f => f.File.IsInRepo)) +
                                    r.Machines.Count(m => m.Disks.Count > 0  &&
                                                          m.Files.Count == 0 &&
                                                          m.Disks.All(f => f.Disk.IsInRepo)) +
                                    r.Machines.Count(m => m.Files.Count > 0                 &&
                                                          m.Disks.Count > 0                 &&
                                                          m.Files.All(f => f.File.IsInRepo) &&
                                                          m.Disks.All(f => f.Disk.IsInRepo)),
                                IncompleteMachines =
                                    r.Machines.Count(m => m.Files.Count > 0  &&
                                                          m.Disks.Count == 0 &&
                                                          m.Files.Any(f => !f.File.IsInRepo)) +
                                    r.Machines.Count(m => m.Disks.Count > 0  &&
                                                          m.Files.Count == 0 &&
                                                          m.Disks.Any(f => !f.Disk.IsInRepo)) +
                                    r.Machines.Count(m => m.Files.Count > 0 &&
                                                          m.Disks.Count > 0 &&
                                                          (m.Files.Any(f => !f.File.IsInRepo) ||
                                                           m.Disks.Any(f => !f.Disk.IsInRepo))),
                                TotalRoms =
                                    r.Machines.Sum(m => m.Files.Count) +
                                    r.Machines.Sum(m => m.Disks.Count) +
                                    r.Machines.Sum(m => m.Medias.Count),
                                HaveRoms = r.Machines.Sum(m => m.Files.Count(f => f.File.IsInRepo)) +
                                           r.Machines.Sum(m => m.Disks.Count(f => f.Disk.IsInRepo)) +
                                           r.Machines.Sum(m => m.Medias.Count(f => f.Media.IsInRepo)),
                                MissRoms = r.Machines.Sum(m => m.Files.Count(f => !f.File.IsInRepo)) +
                                           r.Machines.Sum(m => m.Disks.Count(f => !f.Disk.IsInRepo)) +
                                           r.Machines.Sum(m => m.Medias.Count(f => !f.Media.IsInRepo))
                            })
                           .FirstOrDefault();

                RomSetStat oldStats = ctx.RomSetStats.Find(stats.RomSetId);

                if(oldStats != null) ctx.Remove(oldStats);

                ctx.RomSetStats.Add(stats);

                ctx.SaveChanges();
            }

            WorkFinished?.Invoke(this,
                                 new MessageEventArgs
                                 {
                                     Message = string.Format(Localization.DatImportSuccess,
                                                             stats.TotalMachines,
                                                             stats.TotalRoms)
                                 });

            RomSetAdded?.Invoke(this,
                                new RomSetEventArgs
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
            if(Debugger.IsAttached) throw;

            ErrorOccurred?.Invoke(this,
                                  new ErrorEventArgs
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