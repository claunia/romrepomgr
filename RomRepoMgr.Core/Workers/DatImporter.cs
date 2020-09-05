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
using System.Diagnostics;
using System.IO;
using System.Linq;
using Aaru.Checksums;
using RomRepoMgr.Core.EventArgs;
using RomRepoMgr.Core.Models;
using RomRepoMgr.Core.Resources;
using RomRepoMgr.Database;
using RomRepoMgr.Database.Models;
using SabreTools.Library.DatFiles;
using SabreTools.Library.DatItems;
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

                var datFile = DatFile.CreateAndParse(_datPath);

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
                        RomSet    = romSet,
                        CreatedOn = DateTime.UtcNow,
                        UpdatedOn = DateTime.UtcNow
                    };

                    machines[name] = machine;

                    ctx.Machines.Add(machine);
                    position++;
                }

                SetMessage?.Invoke(this, new MessageEventArgs
                {
                    Message = Localization.SavingChangesToDatabase
                });

                SetIndeterminateProgress?.Invoke(this, System.EventArgs.Empty);

                ctx.SaveChanges();

                SetMessage?.Invoke(this, new MessageEventArgs
                {
                    Message = Localization.RetrievingRomsAndDisks
                });

                List<Rom>   roms   = new List<Rom>();
                List<Disk>  disks  = new List<Disk>();
                List<Media> medias = new List<Media>();

                foreach(List<DatItem> values in datFile.Items.Values)
                {
                    foreach(DatItem item in values)
                    {
                        switch(item)
                        {
                            case Rom rom:
                                roms.Add(rom);

                                continue;
                            case Disk disk:
                                disks.Add(disk);

                                continue;
                            case Media media:
                                medias.Add(media);

                                continue;
                        }
                    }
                }

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

                Dictionary<string, DbFile> pendingFilesBySha512 = new Dictionary<string, DbFile>();
                Dictionary<string, DbFile> pendingFilesBySha384 = new Dictionary<string, DbFile>();
                Dictionary<string, DbFile> pendingFilesBySha256 = new Dictionary<string, DbFile>();
                Dictionary<string, DbFile> pendingFilesBySha1   = new Dictionary<string, DbFile>();
                Dictionary<string, DbFile> pendingFilesByMd5    = new Dictionary<string, DbFile>();
                Dictionary<string, DbFile> pendingFilesByCrc    = new Dictionary<string, DbFile>();
                List<DbFile>               pendingFiles         = new List<DbFile>();
                List<DbFile>               newFiles             = new List<DbFile>();
                List<FileByMachine>        newFilesByMachine    = new List<FileByMachine>();

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

                    file ??= ctx.Files.FirstOrDefault(f => ((rom.SHA512 != null && f.Sha512 == rom.SHA512) ||
                                                            (rom.SHA384 != null && f.Sha384 == rom.SHA384) ||
                                                            (rom.SHA256 != null && f.Sha256 == rom.SHA256) ||
                                                            (rom.SHA1   != null && f.Sha1   == rom.SHA1)   ||
                                                            (rom.MD5    != null && f.Md5    == rom.MD5)    ||
                                                            (rom.CRC    != null && f.Crc32  == rom.CRC)) &&
                                                           f.Size == uSize);

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

                    newFilesByMachine.Add(new FileByMachine
                    {
                        File    = file,
                        Machine = machine,
                        Name    = rom.Name
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

                ctx.Files.AddRange(newFiles);
                ctx.FilesByMachines.AddRange(newFilesByMachine);

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

                Dictionary<string, DbDisk> pendingDisksBySha1 = new Dictionary<string, DbDisk>();
                Dictionary<string, DbDisk> pendingDisksByMd5  = new Dictionary<string, DbDisk>();
                List<DbDisk>               newDisks           = new List<DbDisk>();
                List<DiskByMachine>        newDisksByMachine  = new List<DiskByMachine>();

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

                    dbDisk ??= ctx.Disks.FirstOrDefault(f => (disk.SHA1 != null && f.Sha1 == disk.SHA1) ||
                                                             (disk.MD5  != null && f.Md5  == disk.MD5));

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

                ctx.Disks.AddRange(newDisks);
                ctx.DisksByMachines.AddRange(newDisksByMachine);

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

                Dictionary<string, DbMedia> pendingMediasBySha256 = new Dictionary<string, DbMedia>();
                Dictionary<string, DbMedia> pendingMediasBySha1   = new Dictionary<string, DbMedia>();
                Dictionary<string, DbMedia> pendingMediasByMd5    = new Dictionary<string, DbMedia>();
                List<DbMedia>               newMedias             = new List<DbMedia>();
                List<MediaByMachine>        newMediasByMachine    = new List<MediaByMachine>();

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

                    dbMedia ??= ctx.Medias.FirstOrDefault(f => (media.SHA256 != null && f.Sha256 == media.SHA256) ||
                                                               (media.SHA1   != null && f.Sha1   == media.SHA1)   ||
                                                               (media.MD5    != null && f.Md5    == media.MD5));

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

                ctx.Medias.AddRange(newMedias);
                ctx.MediasByMachines.AddRange(newMediasByMachine);

                ctx.SaveChanges();

                pendingMediasBySha256.Clear();
                pendingMediasBySha1.Clear();
                pendingMediasByMd5.Clear();
                newMedias.Clear();
                newMediasByMachine.Clear();

                WorkFinished?.Invoke(this, System.EventArgs.Empty);

                romSet = ctx.RomSets.Find(romSet.Id);

                RomSetAdded?.Invoke(this, new RomSetEventArgs
                {
                    RomSet = new RomSetModel
                    {
                        Id            = romSet.Id,
                        Author        = romSet.Author,
                        Comment       = romSet.Comment,
                        Date          = romSet.Date,
                        Description   = romSet.Description,
                        Filename      = romSet.Filename,
                        Homepage      = romSet.Homepage,
                        Name          = romSet.Name,
                        Sha384        = romSet.Sha384,
                        Version       = romSet.Version,
                        TotalMachines = romSet.Machines?.Count ?? 0,
                        CompleteMachines =
                            romSet.Machines?.Count(m => m.Files?.Count > 0 && m.Disks == null &&
                                                        m.Files.All(f => f.File.IsInRepo)) ??
                            0 + romSet.Machines?.Count(m => m.Disks?.Count > 0 && m.Files == null &&
                                                            m.Disks.All(f => f.Disk.IsInRepo)) ?? 0 +
                            romSet.Machines?.Count(m => m.Files?.Count > 0                && m.Disks?.Count > 0 &&
                                                        m.Files.All(f => f.File.IsInRepo) &&
                                                        m.Disks.All(f => f.Disk.IsInRepo)) ?? 0,
                        IncompleteMachines =
                            romSet.Machines?.Count(m => m.Files?.Count > 0 && m.Disks == null &&
                                                        m.Files.Any(f => !f.File.IsInRepo)) ??
                            0 + romSet.Machines?.Count(m => m.Disks?.Count > 0 && m.Files == null &&
                                                            m.Disks.Any(f => !f.Disk.IsInRepo)) ?? 0 +
                            romSet.Machines?.Count(m => m.Files?.Count > 0 && m.Disks?.Count > 0 &&
                                                        (m.Files.Any(f => !f.File.IsInRepo) ||
                                                         m.Disks.Any(f => !f.Disk.IsInRepo))) ?? 0,
                        TotalRoms = romSet.Machines?.Sum(m => m.Files?.Count ?? 0) ??
                                    0 + romSet.Machines?.Sum(m => m.Disks?.Count ?? 0) ??
                                    0 + romSet.Machines?.Sum(m => m.Medias?.Count ?? 0) ?? 0,
                        HaveRoms = romSet.Machines?.Sum(m => m.Files?.Count(f => f.File.IsInRepo) ?? 0) ??
                                   0 + romSet.Machines?.Sum(m => m.Disks?.Count(f => f.Disk.IsInRepo) ?? 0) ??
                                   0 + romSet.Machines?.Sum(m => m.Medias?.Count(f => f.Media.IsInRepo) ?? 0) ?? 0,
                        MissRoms = romSet.Machines?.Sum(m => m.Files?.Count(f => !f.File.IsInRepo) ?? 0) ??
                                   0 + romSet.Machines?.Sum(m => m.Disks?.Count(f => !f.Disk.IsInRepo) ?? 0) ??
                                   0 + romSet.Machines?.Sum(m => m.Medias?.Count(f => !f.Media.IsInRepo) ?? 0) ?? 0,
                        Category = _category
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
        public event EventHandler                          WorkFinished;
        public event EventHandler<ErrorEventArgs>          ErrorOccurred;
        public event EventHandler<ProgressBoundsEventArgs> SetProgressBounds;
        public event EventHandler<ProgressEventArgs>       SetProgress;
        public event EventHandler<MessageEventArgs>        SetMessage;
        public event EventHandler<RomSetEventArgs>         RomSetAdded;
    }
}