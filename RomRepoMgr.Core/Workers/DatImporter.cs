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
        readonly string _datFilesPath;
        readonly string _datPath;
        bool            _aborted;

        public DatImporter(string datPath)
        {
            _datPath      = datPath;
            _datFilesPath = Path.Combine(Settings.Settings.Current.RepositoryPath, "datfiles");
        }

        public void Import()
        {
            try
            {
                SetIndeterminateProgress?.Invoke(this, System.EventArgs.Empty);

                SetMessage?.Invoke(this, new MessageEventArgs
                {
                    Message = "Parsing DAT file..."
                });

                var datFile = DatFile.CreateAndParse(_datPath);

                SetMessage?.Invoke(this, new MessageEventArgs
                {
                    Message = "Hashing DAT file..."
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
                        Message = "DAT file is already in database, not importing duplicates."
                    });

                    return;
                }

                SetMessage?.Invoke(this, new MessageEventArgs
                {
                    Message = "Adding DAT to database..."
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
                    UpdatedOn   = DateTime.UtcNow
                };

                Context.Singleton.RomSets.Add(romSet);
                Context.Singleton.SaveChanges();

                RomSetAdded?.Invoke(this, new RomSetEventArgs
                {
                    RomSet = new RomSetModel
                    {
                        Author      = romSet.Author,
                        Comment     = romSet.Comment,
                        Date        = romSet.Date,
                        Description = romSet.Description,
                        Filename    = romSet.Filename,
                        Homepage    = romSet.Homepage,
                        Name        = romSet.Name,
                        Sha384      = romSet.Sha384,
                        Version     = romSet.Version
                    }
                });

                SetMessage?.Invoke(this, new MessageEventArgs
                {
                    Message = "Compressing DAT file..."
                });

                var datCompress = new Compression();
                datCompress.SetProgress       += SetProgress;
                datCompress.SetProgressBounds += SetProgressBounds;
                datCompress.CompressFile(_datPath, compressedDatPath);

                SetMessage?.Invoke(this, new MessageEventArgs
                {
                    Message = "Getting machine (game) names..."
                });

                List<string> machineNames =
                    (from value in datFile.Items.Values from item in value select item.Machine.Name).Distinct().
                    ToList();

                SetMessage?.Invoke(this, new MessageEventArgs
                {
                    Message = "Adding machines (games)..."
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

                    Context.Singleton.Machines.Add(machine);
                    position++;
                }

                SetMessage?.Invoke(this, new MessageEventArgs
                {
                    Message = "Saving changes to database..."
                });

                SetIndeterminateProgress?.Invoke(this, System.EventArgs.Empty);

                Context.Singleton.SaveChanges();

                SetMessage?.Invoke(this, new MessageEventArgs
                {
                    Message = "Retrieving ROMs and disks..."
                });

                List<Rom>  roms  = new List<Rom>();
                List<Disk> disks = new List<Disk>();

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
                    Message = "Adding ROMs..."
                });

                position = 0;

                Dictionary<string, DbFile> pendingFilesBySha512 = new Dictionary<string, DbFile>();
                Dictionary<string, DbFile> pendingFilesBySha384 = new Dictionary<string, DbFile>();
                Dictionary<string, DbFile> pendingFilesBySha256 = new Dictionary<string, DbFile>();
                Dictionary<string, DbFile> pendingFilesBySha1   = new Dictionary<string, DbFile>();
                Dictionary<string, DbFile> pendingFilesByMd5    = new Dictionary<string, DbFile>();
                Dictionary<string, DbFile> pendingFilesByCrc    = new Dictionary<string, DbFile>();
                List<DbFile>               pendingFiles         = new List<DbFile>();

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
                            Message = "Found a ROM with an unknown machine, this should not happen."
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

                    if(file       == null &&
                       rom.SHA512 != null)
                        file = Context.Singleton.Files.FirstOrDefault(f => f.Sha512 == rom.SHA512 && f.Size == uSize);

                    if(file       == null &&
                       rom.SHA384 != null)
                        file = Context.Singleton.Files.FirstOrDefault(f => f.Sha384 == rom.SHA384 && f.Size == uSize);

                    if(file       == null &&
                       rom.SHA256 != null)
                        file = Context.Singleton.Files.FirstOrDefault(f => f.Sha256 == rom.SHA256 && f.Size == uSize);

                    if(file     == null &&
                       rom.SHA1 != null)
                        file = Context.Singleton.Files.FirstOrDefault(f => f.Sha1 == rom.SHA1 && f.Size == uSize);

                    if(file    == null &&
                       rom.MD5 != null)
                        file = Context.Singleton.Files.FirstOrDefault(f => f.Md5 == rom.MD5 && f.Size == uSize);

                    if(file    == null &&
                       rom.CRC != null)
                        file = Context.Singleton.Files.FirstOrDefault(f => f.Crc32 == rom.CRC && f.Size == uSize);

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

                        Context.Singleton.Files.Add(file);
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

                    Context.Singleton.FilesByMachines.Add(new FileByMachine
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
                    Message = "Saving changes to database..."
                });

                SetIndeterminateProgress?.Invoke(this, System.EventArgs.Empty);

                Context.Singleton.SaveChanges();

                SetProgressBounds?.Invoke(this, new ProgressBoundsEventArgs
                {
                    Minimum = 0,
                    Maximum = disks.Count
                });

                // TODO: Support CHDs
                SetMessage?.Invoke(this, new MessageEventArgs
                {
                    Message = "Adding disks..."
                });

                WorkFinished?.Invoke(this, System.EventArgs.Empty);
            }
            catch(Exception e)
            {
                if(Debugger.IsAttached)
                    throw;

                ErrorOccurred?.Invoke(this, new ErrorEventArgs
                {
                    Message = "Unhandled exception occurred."
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