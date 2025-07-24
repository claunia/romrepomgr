using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using RomRepoMgr.Core.Aaru;
using RomRepoMgr.Core.EventArgs;
using RomRepoMgr.Core.Models;
using RomRepoMgr.Core.Resources;
using RomRepoMgr.Database;
using RomRepoMgr.Database.Models;
using SabreTools.FileTypes.Aaru;
using SabreTools.FileTypes.CHD;
using SharpCompress.Compressors.LZMA;
using CompressionMode = SharpCompress.Compressors.CompressionMode;

namespace RomRepoMgr.Core.Workers;

public sealed class FileImporter(bool onlyKnown, bool deleteAfterImport)
{
    const           long                        BUFFER_SIZE = 131072;
    static readonly Lock                        DbLock = new();
    readonly        Context                     _ctx = Context.Create(Settings.Settings.Current.DatabasePath);
    readonly        List<DbDisk>                _newDisks = [];
    readonly        List<DbFile>                _newFiles = [];
    readonly        List<DbMedia>               _newMedias = [];
    readonly        Dictionary<string, DbDisk>  _pendingDisksByMd5 = [];
    readonly        Dictionary<string, DbDisk>  _pendingDisksBySha1 = [];
    readonly        Dictionary<string, DbFile>  _pendingFiles = [];
    readonly        Dictionary<string, DbMedia> _pendingMediasByMd5 = [];
    readonly        Dictionary<string, DbMedia> _pendingMediasBySha1 = [];
    readonly        Dictionary<string, DbMedia> _pendingMediasBySha256 = [];
    string                                      _archiveFolder;
    string                                      _lastMessage;
    long                                        _position;
    long                                        _totalFiles;

    public List<string> Files    { get; private set; } = [];
    public List<string> Archives { get; private set; } = [];

    public event EventHandler                           SetIndeterminateProgress2;
    public event EventHandler<ProgressBoundsEventArgs>  SetProgressBounds2;
    public event EventHandler<ProgressEventArgs>        SetProgress2;
    public event EventHandler<MessageEventArgs>         SetMessage2;
    public event EventHandler                           SetIndeterminateProgress;
    public event EventHandler<ProgressBoundsEventArgs>  SetProgressBounds;
    public event EventHandler<ProgressEventArgs>        SetProgress;
    public event EventHandler<MessageEventArgs>         SetMessage;
    public event EventHandler                           Finished;
    public event EventHandler<ImportedRomItemEventArgs> ImportedRom;
    public event EventHandler<MessageEventArgs>         WorkFinished;

    public void FindFiles(string path)
    {
        SetIndeterminateProgress?.Invoke(this, System.EventArgs.Empty);

        SetMessage?.Invoke(this,
                           new MessageEventArgs
                           {
                               Message = Localization.EnumeratingFiles
                           });

        Files = Directory.GetFiles(path, "*", SearchOption.AllDirectories).Order().ToList();

        SetMessage?.Invoke(this,
                           new MessageEventArgs
                           {
                               Message = "Finished enumerating files. Found " + Files.Count + " files."
                           });

        Finished?.Invoke(this, System.EventArgs.Empty);
    }

    public void SeparateFilesAndArchives()
    {
        SetProgressBounds?.Invoke(this,
                                  new ProgressBoundsEventArgs
                                  {
                                      Minimum = 0,
                                      Maximum = Files.Count
                                  });

        ConcurrentBag<string> files    = [];
        ConcurrentBag<string> archives = [];

        Parallel.ForEach(Files,
                         file =>
                         {
                             try
                             {
                                 SetProgress?.Invoke(this,
                                                     new ProgressEventArgs
                                                     {
                                                         Value = _position
                                                     });

                                 SetMessage?.Invoke(this,
                                                    new MessageEventArgs
                                                    {
                                                        Message = "Checking archives. Found " +
                                                                  archives.Count              +
                                                                  " archives and "            +
                                                                  files.Count                 +
                                                                  " files."
                                                    });

                                 SetMessage2?.Invoke(this,
                                                     new MessageEventArgs
                                                     {
                                                         Message =
                                                             $"Checking if file {Path.GetFileName(file)} is an archive..."
                                                     });

                                 SetIndeterminateProgress2?.Invoke(this, System.EventArgs.Empty);

                                 string archiveFormat = GetArchiveFormat(file, out _);

                                 // If a floppy contains only the archive, unar will recognize it, on its skipping of SFXs.
                                 if(archiveFormat != null && FAT.Identify(file)) archiveFormat = null;

                                 if(archiveFormat != null)
                                     archives.Add(file);
                                 else
                                     files.Add(file);

                                 Interlocked.Increment(ref _position);
                             }
                             catch(Exception ex)
                             {
                                 Console.WriteLine("Exception while checking file {0}: {1}",
                                                   Path.GetFileName(file),
                                                   ex.Message);
                             }
                         });

        Files    = files.Order().ToList();
        Archives = archives.Order().ToList();

        SetMessage?.Invoke(this,
                           new MessageEventArgs
                           {
                               Message = "Finished checking archives. Found " +
                                         Archives.Count                       +
                                         " archives and "                     +
                                         Files.Count                          +
                                         " files."
                           });

        Finished?.Invoke(this, System.EventArgs.Empty);
    }

    public void ImportFile(string file)
    {
        SetMessage2?.Invoke(this,
                            new MessageEventArgs
                            {
                                Message = string.Format(Localization.Importing, Path.GetFileName(file))
                            });

        var fs = new FileStream(file, FileMode.Open, FileAccess.Read);

        var aif = AaruFormat.Create(fs);

        bool ret;

        if(aif != null)
        {
            fs.Close();

            ret = ImportMedia(file);

            if(ret)
            {
                ImportedRom?.Invoke(this,
                                    new ImportedRomItemEventArgs
                                    {
                                        Item = new ImportRomItem
                                        {
                                            Filename = Path.GetFileName(file),
                                            Status   = Localization.OK
                                        }
                                    });
            }
            else
            {
                ImportedRom?.Invoke(this,
                                    new ImportedRomItemEventArgs
                                    {
                                        Item = new ImportRomItem
                                        {
                                            Filename = Path.GetFileName(file),
                                            Status   = string.Format(Localization.ErrorWithMessage, _lastMessage)
                                        }
                                    });
            }

            return;
        }

        fs.Position = 0;

        var chd = CHDFile.Create(fs);

        if(chd != null)
        {
            fs.Close();

            ret = ImportDisk(file);

            if(ret)
            {
                ImportedRom?.Invoke(this,
                                    new ImportedRomItemEventArgs
                                    {
                                        Item = new ImportRomItem
                                        {
                                            Filename = Path.GetFileName(file),
                                            Status   = Localization.OK
                                        }
                                    });
            }
            else
            {
                ImportedRom?.Invoke(this,
                                    new ImportedRomItemEventArgs
                                    {
                                        Item = new ImportRomItem
                                        {
                                            Filename = Path.GetFileName(file),
                                            Status   = string.Format(Localization.ErrorWithMessage, _lastMessage)
                                        }
                                    });
            }

            return;
        }

        fs.Close();

        ret = ImportRom(file);

        if(ret)
        {
            ImportedRom?.Invoke(this,
                                new ImportedRomItemEventArgs
                                {
                                    Item = new ImportRomItem
                                    {
                                        Filename = Path.GetFileName(file),
                                        Status   = Localization.OK
                                    }
                                });
        }
        else
        {
            ImportedRom?.Invoke(this,
                                new ImportedRomItemEventArgs
                                {
                                    Item = new ImportRomItem
                                    {
                                        Filename = Path.GetFileName(file),
                                        Status   = string.Format(Localization.ErrorWithMessage, _lastMessage)
                                    }
                                });
        }
    }

    public bool ExtractArchive(string archive)
    {
        string archiveFormat = null;
        long   archiveFiles  = 0;

        SetIndeterminateProgress2?.Invoke(this, System.EventArgs.Empty);

        SetMessage2?.Invoke(this,
                            new MessageEventArgs
                            {
                                Message = Localization.CheckingIfFIleIsAnArchive
                            });

        archiveFormat = GetArchiveFormat(archive, out archiveFiles);

        // If a floppy contains only the archive, unar will recognize it, on its skipping of SFXs.
        if(archiveFormat != null && FAT.Identify(archive)) archiveFormat = null;

        if(archiveFormat is null) return false;

        if(!Directory.Exists(Settings.Settings.Current.TemporaryFolder))
            Directory.CreateDirectory(Settings.Settings.Current.TemporaryFolder);

        _archiveFolder = Path.Combine(Settings.Settings.Current.TemporaryFolder, Path.GetRandomFileName());

        Directory.CreateDirectory(_archiveFolder);

        SetProgressBounds2?.Invoke(this,
                                   new ProgressBoundsEventArgs
                                   {
                                       Minimum = 0,
                                       Maximum = archiveFiles
                                   });

        SetMessage2?.Invoke(this,
                            new MessageEventArgs
                            {
                                Message = Localization.ExtractingArchive
                            });

        if(archiveFormat is "Zip")
            ZipFile.ExtractToDirectory(archive, _archiveFolder);
        else
            ExtractArchive(archive, _archiveFolder);

        Files = Directory.GetFiles(_archiveFolder, "*", SearchOption.AllDirectories).Order().ToList();

        SetMessage2?.Invoke(this,
                            new MessageEventArgs
                            {
                                Message = "Finished extracting files. Extracted " + Files.Count + " files."
                            });

        return true;
    }

    public void CleanupExtractedArchive()
    {
        SetIndeterminateProgress2?.Invoke(this, System.EventArgs.Empty);

        SetMessage2?.Invoke(this,
                            new MessageEventArgs
                            {
                                Message = Localization.RemovingTemporaryPath
                            });

        try
        {
            Directory.Delete(_archiveFolder, true);
        }
        catch(Exception e)
        {
            Console.WriteLine(e);

            // Show must go on
        }
    }

    public void UpdateRomStats()
    {
        SetIndeterminateProgress2?.Invoke(this, System.EventArgs.Empty);

        SetMessage2?.Invoke(this,
                            new MessageEventArgs
                            {
                                Message = Localization.SavingChangesToDatabase
                            });

        lock(DbLock)
        {
            _ctx.SaveChanges();

            _ctx.Database.ExecuteSqlRaw("DELETE FROM \"RomSetStats\"");

            _ctx.RomSetStats.AddRange(_ctx.RomSets.OrderBy(r => r.Id)
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
                                           }));

            // TODO: Refresh main view

            _ctx.SaveChanges();
        }
    }

    bool ImportRom(string path)
    {
        try
        {
            var inFs = new FileStream(path, FileMode.Open, FileAccess.Read);

            byte[] buffer;

            SetMessage2?.Invoke(this,
                                new MessageEventArgs
                                {
                                    Message = Localization.HashingFile
                                });

            var checksumWorker = new Checksum();

            if(inFs.Length > BUFFER_SIZE)
            {
                SetProgressBounds2?.Invoke(this,
                                           new ProgressBoundsEventArgs
                                           {
                                               Minimum = 0,
                                               Maximum = inFs.Length
                                           });

                long offset;
                long remainder = inFs.Length % BUFFER_SIZE;

                for(offset = 0; offset < inFs.Length - remainder; offset += (int)BUFFER_SIZE)
                {
                    SetProgress2?.Invoke(this,
                                         new ProgressEventArgs
                                         {
                                             Value = offset
                                         });

                    buffer = new byte[BUFFER_SIZE];
                    inFs.EnsureRead(buffer, 0, (int)BUFFER_SIZE);
                    checksumWorker.Update(buffer);
                }

                SetProgress2?.Invoke(this,
                                     new ProgressEventArgs
                                     {
                                         Value = offset
                                     });

                buffer = new byte[remainder];
                inFs.EnsureRead(buffer, 0, (int)remainder);
            }
            else
            {
                SetIndeterminateProgress2?.Invoke(this, System.EventArgs.Empty);
                buffer = new byte[inFs.Length];
                inFs.EnsureRead(buffer, 0, (int)inFs.Length);
            }

            checksumWorker.Update(buffer);

            Dictionary<ChecksumType, string> checksums = checksumWorker.End();

            ulong uSize    = (ulong)inFs.Length;
            bool  fileInDb = true;

            bool knownFile = _pendingFiles.TryGetValue(checksums[ChecksumType.Sha512], out DbFile dbFile);

            lock(DbLock)
            {
                dbFile ??= _ctx.Files.FirstOrDefault(f => (f.Sha512 == checksums[ChecksumType.Sha512] ||
                                                           f.Sha384 == checksums[ChecksumType.Sha384] ||
                                                           f.Sha256 == checksums[ChecksumType.Sha256] ||
                                                           f.Sha1   == checksums[ChecksumType.Sha1]   ||
                                                           f.Md5    == checksums[ChecksumType.Md5]    ||
                                                           f.Crc32  == checksums[ChecksumType.Crc32]) &&
                                                          f.Size == uSize);
            }

            if(dbFile == null)
            {
                if(onlyKnown)
                {
                    _lastMessage = Localization.UnknownFile;

                    return false;
                }

                dbFile = new DbFile
                {
                    Crc32            = checksums[ChecksumType.Crc32],
                    Md5              = checksums[ChecksumType.Md5],
                    Sha1             = checksums[ChecksumType.Sha1],
                    Sha256           = checksums[ChecksumType.Sha256],
                    Sha384           = checksums[ChecksumType.Sha384],
                    Sha512           = checksums[ChecksumType.Sha512],
                    Size             = uSize,
                    CreatedOn        = DateTime.UtcNow,
                    UpdatedOn        = DateTime.UtcNow,
                    OriginalFileName = Path.GetFileName(path)
                };

                fileInDb = false;
            }

            if(!knownFile) _pendingFiles[checksums[ChecksumType.Sha512]] = dbFile;

            byte[] sha384Bytes = new byte[48];
            string sha384      = checksums[ChecksumType.Sha384];

            for(int i = 0; i < 48; i++)
            {
                if(sha384[i * 2] >= 0x30 && sha384[i * 2] <= 0x39)
                    sha384Bytes[i] = (byte)((sha384[i * 2] - 0x30) * 0x10);
                else if(sha384[i * 2] >= 0x41 && sha384[i * 2] <= 0x46)
                    sha384Bytes[i] = (byte)((sha384[i * 2] - 0x37) * 0x10);
                else if(sha384[i * 2] >= 0x61 && sha384[i * 2] <= 0x66)
                    sha384Bytes[i] = (byte)((sha384[i * 2] - 0x57) * 0x10);

                if(sha384[i * 2 + 1] >= 0x30 && sha384[i * 2 + 1] <= 0x39)
                    sha384Bytes[i] += (byte)(sha384[i * 2 + 1] - 0x30);
                else if(sha384[i * 2 + 1] >= 0x41 && sha384[i * 2 + 1] <= 0x46)
                    sha384Bytes[i] += (byte)(sha384[i * 2 + 1] - 0x37);
                else if(sha384[i * 2 + 1] >= 0x61 && sha384[i * 2 + 1] <= 0x66)
                    sha384Bytes[i] += (byte)(sha384[i * 2 + 1] - 0x57);
            }

            string sha384B32 = Base32.ToBase32String(sha384Bytes);

            string repoPath = Path.Combine(Settings.Settings.Current.RepositoryPath,
                                           "files",
                                           sha384B32[0].ToString(),
                                           sha384B32[1].ToString(),
                                           sha384B32[2].ToString(),
                                           sha384B32[3].ToString(),
                                           sha384B32[4].ToString());

            if(!Directory.Exists(repoPath)) Directory.CreateDirectory(repoPath);

            repoPath = Path.Combine(repoPath, sha384B32 + ".lz");

            if(dbFile.Crc32 == null)
            {
                dbFile.Crc32     = checksums[ChecksumType.Crc32];
                dbFile.UpdatedOn = DateTime.UtcNow;
            }

            if(dbFile.Md5 == null)
            {
                dbFile.Md5       = checksums[ChecksumType.Md5];
                dbFile.UpdatedOn = DateTime.UtcNow;
            }

            if(dbFile.Sha1 == null)
            {
                dbFile.Sha1      = checksums[ChecksumType.Sha1];
                dbFile.UpdatedOn = DateTime.UtcNow;
            }

            if(dbFile.Sha256 == null)
            {
                dbFile.Sha256    = checksums[ChecksumType.Sha256];
                dbFile.UpdatedOn = DateTime.UtcNow;
            }

            if(dbFile.Sha384 == null)
            {
                dbFile.Sha384    = checksums[ChecksumType.Sha384];
                dbFile.UpdatedOn = DateTime.UtcNow;
            }

            if(dbFile.Sha512 == null)
            {
                dbFile.Sha512    = checksums[ChecksumType.Sha512];
                dbFile.UpdatedOn = DateTime.UtcNow;
            }

            if(File.Exists(repoPath))
            {
                dbFile.IsInRepo  = true;
                dbFile.UpdatedOn = DateTime.UtcNow;

                if(!fileInDb) _newFiles.Add(dbFile);

                inFs.Close();

                if(deleteAfterImport) File.Delete(path);

                return true;
            }

            inFs.Position = 0;

            var    outFs   = new FileStream(repoPath, FileMode.CreateNew, FileAccess.Write);
            Stream zStream = new LZipStream(outFs, CompressionMode.Compress);

            SetProgressBounds2?.Invoke(this,
                                       new ProgressBoundsEventArgs
                                       {
                                           Minimum = 0,
                                           Maximum = inFs.Length
                                       });

            SetMessage2?.Invoke(this,
                                new MessageEventArgs
                                {
                                    Message = Localization.CompressingFile
                                });

            buffer = new byte[BUFFER_SIZE];

            while(inFs.Position + BUFFER_SIZE <= inFs.Length)
            {
                SetProgress2?.Invoke(this,
                                     new ProgressEventArgs
                                     {
                                         Value = inFs.Position
                                     });

                inFs.EnsureRead(buffer, 0, buffer.Length);
                zStream.Write(buffer, 0, buffer.Length);
            }

            buffer = new byte[inFs.Length - inFs.Position];

            SetProgress2?.Invoke(this,
                                 new ProgressEventArgs
                                 {
                                     Value = inFs.Position
                                 });

            inFs.EnsureRead(buffer, 0, buffer.Length);
            zStream.Write(buffer, 0, buffer.Length);

            SetIndeterminateProgress2?.Invoke(this, System.EventArgs.Empty);

            SetMessage2?.Invoke(this,
                                new MessageEventArgs
                                {
                                    Message = Localization.Finishing
                                });

            inFs.Close();
            zStream.Close();
            outFs.Dispose();

            dbFile.IsInRepo  = true;
            dbFile.UpdatedOn = DateTime.UtcNow;

            if(!fileInDb) _newFiles.Add(dbFile);

            if(deleteAfterImport) File.Delete(path);

            return true;
        }
        catch(Exception ex)
        {
            _lastMessage = Localization.UnhandledExceptionWhenImporting;

#pragma warning disable ERP022
            return false;
#pragma warning restore ERP022
        }
    }

    bool ImportDisk(string path)
    {
        try
        {
            var inFs = new FileStream(path, FileMode.Open, FileAccess.Read);

            SetMessage2?.Invoke(this,
                                new MessageEventArgs
                                {
                                    Message = Localization.HashingFile
                                });

            var chd = CHDFile.Create(path);

            if(chd == null)
            {
                _lastMessage = Localization.NotAChdFile;

                return false;
            }

            if(chd.MD5 == null && chd.SHA1 == null)
            {
                _lastMessage = Localization.NoChecksumsFound;

                return false;
            }

            string md5  = null;
            string sha1 = null;

            if(chd.MD5 != null)
            {
                char[] chdArray = new char[32];

                for(int i = 0; i < 16; i++)
                {
                    int nibble1 = chd.MD5[i] >> 4;
                    int nibble2 = chd.MD5[i] & 0xF;

                    nibble1 += nibble1 >= 0xA ? 0x37 : 0x30;
                    nibble2 += nibble2 >= 0xA ? 0x37 : 0x30;

                    chdArray[i * 2]     = (char)nibble1;
                    chdArray[i * 2 + 1] = (char)nibble2;
                }

                md5 = new string(chdArray);
            }

            if(chd.SHA1 != null)
            {
                char[] chdArray = new char[40];

                for(int i = 0; i < 20; i++)
                {
                    int nibble1 = chd.SHA1[i] >> 4;
                    int nibble2 = chd.SHA1[i] & 0xF;

                    nibble1 += nibble1 >= 0xA ? 0x57 : 0x30;
                    nibble2 += nibble2 >= 0xA ? 0x57 : 0x30;

                    chdArray[i * 2]     = (char)nibble1;
                    chdArray[i * 2 + 1] = (char)nibble2;
                }

                sha1 = new string(chdArray);
            }

            ulong  uSize              = (ulong)inFs.Length;
            bool   diskInDb           = true;
            DbDisk dbDisk             = null;
            bool   knownDisk          = false;
            bool   knownDiskWasBigger = false;

            if(sha1 != null) knownDisk = _pendingDisksBySha1.TryGetValue(sha1, out dbDisk);

            if(!knownDisk && md5 != null) knownDisk = _pendingDisksByMd5.TryGetValue(md5, out dbDisk);

            lock(DbLock)
            {
                dbDisk ??= _ctx.Disks.FirstOrDefault(d => d.Sha1 != null && d.Sha1 == sha1 ||
                                                          d.Md5  != null && d.Md5  == sha1);
            }

            if(dbDisk == null)
            {
                if(onlyKnown)
                {
                    _lastMessage = Localization.UnknownFile;

                    return false;
                }

                dbDisk = new DbDisk
                {
                    Md5              = md5,
                    Sha1             = sha1,
                    Size             = uSize,
                    CreatedOn        = DateTime.UtcNow,
                    UpdatedOn        = DateTime.UtcNow,
                    OriginalFileName = Path.GetFileName(path)
                };

                diskInDb = false;
            }

            if(!knownDisk)
            {
                if(sha1 != null)
                    _pendingDisksBySha1[sha1]                = dbDisk;
                else if(md5 != null) _pendingDisksByMd5[md5] = dbDisk;
            }

            string sha1B32 = null;
            string md5B32  = null;

            if(chd.SHA1 != null) sha1B32 = Base32.ToBase32String(chd.SHA1);

            if(chd.MD5 != null) md5B32 = Base32.ToBase32String(chd.SHA1);

            if(dbDisk.Md5 == null && md5 != null)
            {
                dbDisk.Md5       = md5;
                dbDisk.UpdatedOn = DateTime.UtcNow;
            }

            if(dbDisk.Sha1 == null && sha1 != null)
            {
                dbDisk.Sha1      = sha1;
                dbDisk.UpdatedOn = DateTime.UtcNow;
            }

            if(dbDisk.Size > uSize)
            {
                knownDiskWasBigger = true;
                dbDisk.Size        = null;
            }

            if(dbDisk.Size == null)
            {
                dbDisk.Size      = uSize;
                dbDisk.UpdatedOn = DateTime.UtcNow;
            }

            string md5Path  = null;
            string sha1Path = null;
            string repoPath = null;

            if(md5 != null)
            {
                md5Path = Path.Combine(Settings.Settings.Current.RepositoryPath,
                                       "chd",
                                       "md5",
                                       md5B32[0].ToString(),
                                       md5B32[1].ToString(),
                                       md5B32[2].ToString(),
                                       md5B32[3].ToString(),
                                       md5B32[4].ToString());

                repoPath = md5Path;

                md5Path = Path.Combine(repoPath, md5B32 + ".chd");
            }

            if(sha1 != null)
            {
                sha1Path = Path.Combine(Settings.Settings.Current.RepositoryPath,
                                        "chd",
                                        "sha1",
                                        sha1B32[0].ToString(),
                                        sha1B32[1].ToString(),
                                        sha1B32[2].ToString(),
                                        sha1B32[3].ToString(),
                                        sha1B32[4].ToString());

                repoPath = sha1Path;

                sha1Path = Path.Combine(repoPath, sha1B32 + ".chd");
            }

            if(!Directory.Exists(repoPath)) Directory.CreateDirectory(repoPath);

            if(File.Exists(md5Path) && sha1Path != null) File.Move(md5Path, sha1Path);

            if(sha1Path != null)
                repoPath                      = sha1Path;
            else if(md5Path != null) repoPath = md5Path;

            if(File.Exists(repoPath))
            {
                if(!knownDiskWasBigger)
                    File.Move(repoPath, repoPath + ".bak", true);
                else
                {
                    dbDisk.IsInRepo  = true;
                    dbDisk.UpdatedOn = DateTime.UtcNow;

                    if(!diskInDb) _newDisks.Add(dbDisk);

                    inFs.Close();

                    if(deleteAfterImport) File.Delete(path);

                    return true;
                }
            }

            inFs.Position = 0;
            var outFs = new FileStream(repoPath, FileMode.CreateNew, FileAccess.Write);

            SetProgressBounds2?.Invoke(this,
                                       new ProgressBoundsEventArgs
                                       {
                                           Minimum = 0,
                                           Maximum = inFs.Length
                                       });

            SetMessage2?.Invoke(this,
                                new MessageEventArgs
                                {
                                    Message = Localization.CopyingFile
                                });

            byte[] buffer = new byte[BUFFER_SIZE];

            while(inFs.Position + BUFFER_SIZE <= inFs.Length)
            {
                SetProgress2?.Invoke(this,
                                     new ProgressEventArgs
                                     {
                                         Value = inFs.Position
                                     });

                inFs.EnsureRead(buffer, 0, buffer.Length);
                outFs.Write(buffer, 0, buffer.Length);
            }

            buffer = new byte[inFs.Length - inFs.Position];

            SetProgress2?.Invoke(this,
                                 new ProgressEventArgs
                                 {
                                     Value = inFs.Position
                                 });

            inFs.EnsureRead(buffer, 0, buffer.Length);
            outFs.Write(buffer, 0, buffer.Length);

            SetIndeterminateProgress2?.Invoke(this, System.EventArgs.Empty);

            SetMessage2?.Invoke(this,
                                new MessageEventArgs
                                {
                                    Message = Localization.Finishing
                                });

            inFs.Close();
            outFs.Close();

            dbDisk.IsInRepo  = true;
            dbDisk.UpdatedOn = DateTime.UtcNow;

            if(!diskInDb) _newDisks.Add(dbDisk);

            if(deleteAfterImport) File.Delete(path);

            if(knownDiskWasBigger) File.Delete(repoPath + ".bak");

            return true;
        }
        catch
        {
            _lastMessage = Localization.UnhandledExceptionWhenImporting;

#pragma warning disable ERP022
            return false;
#pragma warning restore ERP022
        }
    }

    bool ImportMedia(string path)
    {
        try
        {
            var inFs = new FileStream(path, FileMode.Open, FileAccess.Read);

            SetMessage2?.Invoke(this,
                                new MessageEventArgs
                                {
                                    Message = Localization.HashingFile
                                });

            var aif = AaruFormat.Create(path);

            if(aif == null)
            {
                _lastMessage = Localization.NotAnAaruFormatFile;

                return false;
            }

            if(aif.MD5 == null && aif.SHA1 == null && aif.SHA256 == null)
            {
                _lastMessage = Localization.NoChecksumsFound;

                return false;
            }

            string md5    = null;
            string sha1   = null;
            string sha256 = null;

            if(aif.MD5 != null)
            {
                char[] chdArray = new char[32];

                for(int i = 0; i < 16; i++)
                {
                    int nibble1 = aif.MD5[i] >> 4;
                    int nibble2 = aif.MD5[i] & 0xF;

                    nibble1 += nibble1 >= 0xA ? 0x37 : 0x30;
                    nibble2 += nibble2 >= 0xA ? 0x37 : 0x30;

                    chdArray[i * 2]     = (char)nibble1;
                    chdArray[i * 2 + 1] = (char)nibble2;
                }

                md5 = new string(chdArray);
            }

            if(aif.SHA1 != null)
            {
                char[] chdArray = new char[40];

                for(int i = 0; i < 20; i++)
                {
                    int nibble1 = aif.SHA1[i] >> 4;
                    int nibble2 = aif.SHA1[i] & 0xF;

                    nibble1 += nibble1 >= 0xA ? 0x57 : 0x30;
                    nibble2 += nibble2 >= 0xA ? 0x57 : 0x30;

                    chdArray[i * 2]     = (char)nibble1;
                    chdArray[i * 2 + 1] = (char)nibble2;
                }

                sha1 = new string(chdArray);
            }

            if(aif.SHA256 != null)
            {
                char[] chdArray = new char[64];

                for(int i = 0; i < 32; i++)
                {
                    int nibble1 = aif.SHA256[i] >> 4;
                    int nibble2 = aif.SHA256[i] & 0xF;

                    nibble1 += nibble1 >= 0xA ? 0x57 : 0x30;
                    nibble2 += nibble2 >= 0xA ? 0x57 : 0x30;

                    chdArray[i * 2]     = (char)nibble1;
                    chdArray[i * 2 + 1] = (char)nibble2;
                }

                sha256 = new string(chdArray);
            }

            ulong   uSize               = (ulong)inFs.Length;
            bool    mediaInDb           = true;
            DbMedia dbMedia             = null;
            bool    knownMedia          = false;
            bool    knownMediaWasBigger = false;

            if(sha256 != null) knownMedia = _pendingMediasBySha256.TryGetValue(sha256, out dbMedia);

            if(!knownMedia && sha1 != null) knownMedia = _pendingMediasBySha1.TryGetValue(sha1, out dbMedia);

            if(!knownMedia && md5 != null) knownMedia = _pendingMediasByMd5.TryGetValue(md5, out dbMedia);

            lock(DbLock)
            {
                dbMedia ??= _ctx.Medias.FirstOrDefault(d => d.Sha256 != null && d.Sha256 == sha256 ||
                                                            d.Sha1   != null && d.Sha1   == sha1   ||
                                                            d.Md5    != null && d.Md5    == sha1);
            }

            if(dbMedia == null)
            {
                if(onlyKnown)
                {
                    _lastMessage = Localization.UnknownFile;

                    return false;
                }

                dbMedia = new DbMedia
                {
                    Md5              = md5,
                    Sha1             = sha1,
                    Sha256           = sha256,
                    Size             = uSize,
                    CreatedOn        = DateTime.UtcNow,
                    UpdatedOn        = DateTime.UtcNow,
                    OriginalFileName = Path.GetFileName(path)
                };

                mediaInDb = false;
            }

            if(!knownMedia)
            {
                if(sha256 != null)
                    _pendingMediasBySha256[sha256] = dbMedia;
                else if(sha1 != null)
                    _pendingMediasBySha1[sha1]                = dbMedia;
                else if(md5 != null) _pendingMediasByMd5[md5] = dbMedia;
            }

            string sha256B32 = null;
            string sha1B32   = null;
            string md5B32    = null;

            if(aif.SHA256 != null) sha256B32 = Base32.ToBase32String(aif.SHA256);

            if(aif.SHA1 != null) sha1B32 = Base32.ToBase32String(aif.SHA1);

            if(aif.MD5 != null) md5B32 = Base32.ToBase32String(aif.SHA1);

            if(dbMedia.Md5 == null && md5 != null)
            {
                dbMedia.Md5       = md5;
                dbMedia.UpdatedOn = DateTime.UtcNow;
            }

            if(dbMedia.Sha1 == null && sha1 != null)
            {
                dbMedia.Sha1      = sha1;
                dbMedia.UpdatedOn = DateTime.UtcNow;
            }

            if(dbMedia.Sha256 == null && sha256 != null)
            {
                dbMedia.Sha256    = sha256;
                dbMedia.UpdatedOn = DateTime.UtcNow;
            }

            if(dbMedia.Size > uSize)
            {
                knownMediaWasBigger = true;
                dbMedia.Size        = null;
            }

            if(dbMedia.Size == null)
            {
                dbMedia.Size      = uSize;
                dbMedia.UpdatedOn = DateTime.UtcNow;
            }

            string md5Path    = null;
            string sha1Path   = null;
            string sha256Path = null;
            string repoPath   = null;

            if(md5 != null)
            {
                md5Path = Path.Combine(Settings.Settings.Current.RepositoryPath,
                                       "aaru",
                                       "md5",
                                       md5B32[0].ToString(),
                                       md5B32[1].ToString(),
                                       md5B32[2].ToString(),
                                       md5B32[3].ToString(),
                                       md5B32[4].ToString());

                repoPath = md5Path;

                md5Path = Path.Combine(repoPath, md5B32 + ".aif");
            }

            if(sha1 != null)
            {
                sha1Path = Path.Combine(Settings.Settings.Current.RepositoryPath,
                                        "aaru",
                                        "sha1",
                                        sha1B32[0].ToString(),
                                        sha1B32[1].ToString(),
                                        sha1B32[2].ToString(),
                                        sha1B32[3].ToString(),
                                        sha1B32[4].ToString());

                repoPath = sha1Path;

                sha1Path = Path.Combine(repoPath, sha1B32 + ".aif");
            }

            if(sha256 != null)
            {
                sha256Path = Path.Combine(Settings.Settings.Current.RepositoryPath,
                                          "aaru",
                                          "sha256",
                                          sha256B32[0].ToString(),
                                          sha256B32[1].ToString(),
                                          sha256B32[2].ToString(),
                                          sha256B32[3].ToString(),
                                          sha256B32[4].ToString());

                repoPath = sha256Path;

                sha256Path = Path.Combine(repoPath, sha256B32 + ".aif");
            }

            if(!Directory.Exists(repoPath)) Directory.CreateDirectory(repoPath);

            if(File.Exists(md5Path))
            {
                if(sha256Path != null)
                    File.Move(md5Path,                       sha256Path);
                else if(sha1Path != null) File.Move(md5Path, sha1Path);
            }

            if(File.Exists(sha1Path) && sha256Path != null) File.Move(sha1Path, sha256Path);

            if(sha256Path != null)
                repoPath = sha256Path;
            else if(sha1Path != null)
                repoPath                      = sha1Path;
            else if(md5Path != null) repoPath = md5Path;

            if(File.Exists(repoPath))
            {
                if(!knownMediaWasBigger)
                    File.Move(repoPath, repoPath + ".bak", true);
                else
                {
                    dbMedia.IsInRepo  = true;
                    dbMedia.UpdatedOn = DateTime.UtcNow;

                    if(!mediaInDb) _newMedias.Add(dbMedia);

                    inFs.Close();

                    if(deleteAfterImport) File.Delete(path);

                    return true;
                }
            }

            inFs.Position = 0;
            var outFs = new FileStream(repoPath, FileMode.CreateNew, FileAccess.Write);

            SetProgressBounds2?.Invoke(this,
                                       new ProgressBoundsEventArgs
                                       {
                                           Minimum = 0,
                                           Maximum = inFs.Length
                                       });

            SetMessage2?.Invoke(this,
                                new MessageEventArgs
                                {
                                    Message = Localization.CopyingFile
                                });

            byte[] buffer = new byte[BUFFER_SIZE];

            while(inFs.Position + BUFFER_SIZE <= inFs.Length)
            {
                SetProgress2?.Invoke(this,
                                     new ProgressEventArgs
                                     {
                                         Value = inFs.Position
                                     });

                inFs.EnsureRead(buffer, 0, buffer.Length);
                outFs.Write(buffer, 0, buffer.Length);
            }

            buffer = new byte[inFs.Length - inFs.Position];

            SetProgress2?.Invoke(this,
                                 new ProgressEventArgs
                                 {
                                     Value = inFs.Position
                                 });

            inFs.EnsureRead(buffer, 0, buffer.Length);
            outFs.Write(buffer, 0, buffer.Length);

            SetIndeterminateProgress2?.Invoke(this, System.EventArgs.Empty);

            SetMessage2?.Invoke(this,
                                new MessageEventArgs
                                {
                                    Message = Localization.Finishing
                                });

            inFs.Close();
            outFs.Close();

            dbMedia.IsInRepo  = true;
            dbMedia.UpdatedOn = DateTime.UtcNow;

            if(!mediaInDb) _newMedias.Add(dbMedia);

            if(deleteAfterImport) File.Delete(path);

            if(knownMediaWasBigger) File.Delete(repoPath + ".bak");

            return true;
        }
        catch
        {
            _lastMessage = Localization.UnhandledExceptionWhenImporting;

#pragma warning disable ERP022
            return false;
#pragma warning restore ERP022
        }
    }

    public void SaveChanges()
    {
        SetIndeterminateProgress2?.Invoke(this, System.EventArgs.Empty);

        SetMessage2?.Invoke(this,
                            new MessageEventArgs
                            {
                                Message = Localization.SavingChangesToDatabase
                            });

        lock(DbLock)
        {
            _ctx.SaveChanges();

            _ctx.Files.AddRange(_newFiles);
            _ctx.Disks.AddRange(_newDisks);
            _ctx.Medias.AddRange(_newMedias);

            _ctx.SaveChanges();
        }

        _newFiles.Clear();
        _newDisks.Clear();
        _newMedias.Clear();

        WorkFinished?.Invoke(this,
                             new MessageEventArgs
                             {
                                 Message = Localization.Finished
                             });
    }

    string GetArchiveFormat(string path, out long counter)
    {
        counter = 0;

        if(!File.Exists(path)) return null;

        try
        {
            string unarFolder   = Path.GetDirectoryName(Settings.Settings.Current.UnArchiverPath);
            string extension    = Path.GetExtension(Settings.Settings.Current.UnArchiverPath);
            string unarFilename = Path.GetFileNameWithoutExtension(Settings.Settings.Current.UnArchiverPath);
            string lsarFilename = unarFilename?.Replace("unar", "lsar");
            string lsarPath     = Path.Combine(unarFolder, lsarFilename + extension);

            var lsarProcess = new Process
            {
                StartInfo =
                {
                    FileName               = lsarPath,
                    CreateNoWindow         = true,
                    RedirectStandardOutput = true,
                    UseShellExecute        = false,
                    ArgumentList =
                    {
                        "-j",
                        path
                    }
                }
            };

            lsarProcess.Start();
            string lsarOutput = lsarProcess.StandardOutput.ReadToEnd();
            lsarProcess.WaitForExit();

            lsar lsar = JsonConvert.DeserializeObject<lsar>(lsarOutput);

            if(lsar is null)
            {
                counter = 0;

                return null;
            }

            counter = lsar.lsarContents.Length;

            return lsar.lsarFormatName;
        }
        catch
        {
#pragma warning disable ERP022
            return null;
#pragma warning restore ERP022
        }
    }

    void ExtractArchive(string archivePath, string outPath)
    {
        var unarProcess = new Process
        {
            StartInfo =
            {
                FileName               = Settings.Settings.Current.UnArchiverPath,
                CreateNoWindow         = true,
                RedirectStandardOutput = true,
                UseShellExecute        = false,
                ArgumentList =
                {
                    "-o",
                    outPath,
                    "-r",
                    "-D",
                    "-k",
                    "hidden",
                    archivePath
                }
            }
        };

        long counter = 0;

        unarProcess.OutputDataReceived += (_, e) =>
        {
            counter++;

            SetMessage2?.Invoke(this,
                                new MessageEventArgs
                                {
                                    Message = e.Data
                                });

            SetProgress2?.Invoke(this,
                                 new ProgressEventArgs
                                 {
                                     Value = counter
                                 });
        };

        unarProcess.Start();
        unarProcess.BeginOutputReadLine();
        unarProcess.WaitForExit();
        unarProcess.Close();
    }
}