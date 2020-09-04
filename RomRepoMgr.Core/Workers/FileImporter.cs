using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using RomRepoMgr.Core.Aaru;
using RomRepoMgr.Core.EventArgs;
using RomRepoMgr.Core.Models;
using RomRepoMgr.Core.Resources;
using RomRepoMgr.Database;
using RomRepoMgr.Database.Models;
using SabreTools.Library.FileTypes;
using SharpCompress.Compressors;
using SharpCompress.Compressors.LZMA;

namespace RomRepoMgr.Core.Workers
{
    public class FileImporter
    {
        const    long                       BUFFER_SIZE = 131072;
        readonly bool                       _deleteAfterImport;
        readonly List<DbDisk>               _newDisks;
        readonly List<DbFile>               _newFiles;
        readonly bool                       _onlyKnown;
        readonly Dictionary<string, DbDisk> _pendingDisksByMd5;
        readonly Dictionary<string, DbDisk> _pendingDisksBySha1;
        readonly Dictionary<string, DbFile> _pendingFiles;
        string                              _lastMessage;
        long                                _position;
        long                                _totalFiles;

        public FileImporter(bool onlyKnown, bool deleteAfterImport)
        {
            _pendingFiles       = new Dictionary<string, DbFile>();
            _pendingDisksByMd5  = new Dictionary<string, DbDisk>();
            _pendingDisksBySha1 = new Dictionary<string, DbDisk>();
            _newFiles           = new List<DbFile>();
            _newDisks           = new List<DbDisk>();
            _onlyKnown          = onlyKnown;
            _deleteAfterImport  = deleteAfterImport;
            _position           = 0;
        }

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

        public void ProcessPath(string path, bool rootPath, bool processArchives)
        {
            try
            {
                SetIndeterminateProgress?.Invoke(this, System.EventArgs.Empty);

                SetMessage?.Invoke(this, new MessageEventArgs
                {
                    Message = Localization.EnumeratingFiles
                });

                string[] files = Directory.GetFiles(path, "*", SearchOption.AllDirectories);
                _totalFiles += files.LongLength;

                SetProgressBounds?.Invoke(this, new ProgressBoundsEventArgs
                {
                    Minimum = 0,
                    Maximum = _totalFiles
                });

                foreach(string file in files)
                {
                    try
                    {
                        SetProgress?.Invoke(this, new ProgressEventArgs
                        {
                            Value = _position
                        });

                        SetMessage?.Invoke(this, new MessageEventArgs
                        {
                            Message = string.Format(Localization.Importing, Path.GetFileName(file))
                        });

                        string archiveFormat = null;
                        long   archiveFiles  = 0;

                        var fs = new FileStream(file, FileMode.Open, FileAccess.Read);

                        var chd = CHDFile.Create(fs);

                        if(chd != null)
                        {
                            fs.Close();

                            bool ret = ImportDisk(file);

                            if(ret)
                            {
                                ImportedRom?.Invoke(this, new ImportedRomItemEventArgs
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
                                ImportedRom?.Invoke(this, new ImportedRomItemEventArgs
                                {
                                    Item = new ImportRomItem
                                    {
                                        Filename = Path.GetFileName(file),
                                        Status   = string.Format(Localization.ErrorWithMessage, _lastMessage)
                                    }
                                });
                            }

                            _position++;

                            continue;
                        }

                        if(processArchives)
                        {
                            SetIndeterminateProgress2?.Invoke(this, System.EventArgs.Empty);

                            SetMessage2?.Invoke(this, new MessageEventArgs
                            {
                                Message = Localization.CheckingIfFIleIsAnArchive
                            });

                            archiveFormat = GetArchiveFormat(file, out archiveFiles);

                            // If a floppy contains only the archive, unar will recognize it, on its skipping of SFXs.
                            if(archiveFormat != null &&
                               FAT.Identify(file))
                                archiveFormat = null;
                        }

                        if(archiveFormat == null)
                        {
                            bool ret = ImportRom(file);

                            if(ret)
                            {
                                ImportedRom?.Invoke(this, new ImportedRomItemEventArgs
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
                                ImportedRom?.Invoke(this, new ImportedRomItemEventArgs
                                {
                                    Item = new ImportRomItem
                                    {
                                        Filename = Path.GetFileName(file),
                                        Status   = string.Format(Localization.ErrorWithMessage, _lastMessage)
                                    }
                                });
                            }
                        }
                        else
                        {
                            if(!Directory.Exists(Settings.Settings.Current.TemporaryFolder))
                                Directory.CreateDirectory(Settings.Settings.Current.TemporaryFolder);

                            string tmpFolder =
                                Path.Combine(Settings.Settings.Current.TemporaryFolder, Path.GetRandomFileName());

                            Directory.CreateDirectory(tmpFolder);

                            SetProgressBounds2?.Invoke(this, new ProgressBoundsEventArgs
                            {
                                Minimum = 0,
                                Maximum = archiveFiles
                            });

                            SetMessage?.Invoke(this, new MessageEventArgs
                            {
                                Message = Localization.ExtractingArchive
                            });

                            ExtractArchive(file, tmpFolder);

                            ProcessPath(tmpFolder, false, true);

                            SetIndeterminateProgress2?.Invoke(this, System.EventArgs.Empty);

                            SetMessage2?.Invoke(this, new MessageEventArgs
                            {
                                Message = Localization.RemovingTemporaryPath
                            });

                            Directory.Delete(tmpFolder, true);

                            ImportedRom?.Invoke(this, new ImportedRomItemEventArgs
                            {
                                Item = new ImportRomItem
                                {
                                    Filename = Path.GetFileName(file),
                                    Status   = Localization.ExtractedContents
                                }
                            });
                        }

                        _position++;
                    }
                    catch(Exception)
                    {
                        ImportedRom?.Invoke(this, new ImportedRomItemEventArgs
                        {
                            Item = new ImportRomItem
                            {
                                Filename = Path.GetFileName(file),
                                Status   = Localization.UnhandledException
                            }
                        });
                    }
                }

                if(!rootPath)
                    return;

                SaveChanges();
                Finished?.Invoke(this, System.EventArgs.Empty);
            }
            catch(Exception)
            {
                // TODO: Send error back
                if(rootPath)
                    Finished?.Invoke(this, System.EventArgs.Empty);
            }
        }

        bool ImportRom(string path)
        {
            try
            {
                var inFs = new FileStream(path, FileMode.Open, FileAccess.Read);

                byte[] buffer;

                SetMessage2?.Invoke(this, new MessageEventArgs
                {
                    Message = Localization.HashingFile
                });

                var checksumWorker = new Checksum();

                if(inFs.Length > BUFFER_SIZE)
                {
                    SetProgressBounds2?.Invoke(this, new ProgressBoundsEventArgs
                    {
                        Minimum = 0,
                        Maximum = inFs.Length
                    });

                    long offset;
                    long remainder = inFs.Length % BUFFER_SIZE;

                    for(offset = 0; offset < inFs.Length - remainder; offset += (int)BUFFER_SIZE)
                    {
                        SetProgress2?.Invoke(this, new ProgressEventArgs
                        {
                            Value = offset
                        });

                        buffer = new byte[BUFFER_SIZE];
                        inFs.Read(buffer, 0, (int)BUFFER_SIZE);
                        checksumWorker.Update(buffer);
                    }

                    SetProgress2?.Invoke(this, new ProgressEventArgs
                    {
                        Value = offset
                    });

                    buffer = new byte[remainder];
                    inFs.Read(buffer, 0, (int)remainder);
                    checksumWorker.Update(buffer);
                }
                else
                {
                    SetIndeterminateProgress2?.Invoke(this, System.EventArgs.Empty);
                    buffer = new byte[inFs.Length];
                    inFs.Read(buffer, 0, (int)inFs.Length);
                    checksumWorker.Update(buffer);
                }

                Dictionary<ChecksumType, string> checksums = checksumWorker.End();

                ulong uSize    = (ulong)inFs.Length;
                bool  fileInDb = true;

                bool knownFile = _pendingFiles.TryGetValue(checksums[ChecksumType.Sha512], out DbFile dbFile);

                dbFile ??= Context.Singleton.Files.FirstOrDefault(f => (f.Sha512 == checksums[ChecksumType.Sha512] ||
                                                                        f.Sha384 == checksums[ChecksumType.Sha384] ||
                                                                        f.Sha256 == checksums[ChecksumType.Sha256] ||
                                                                        f.Sha1   == checksums[ChecksumType.Sha1]   ||
                                                                        f.Md5    == checksums[ChecksumType.Md5]    ||
                                                                        f.Crc32  == checksums[ChecksumType.Crc32]) &&
                                                                       f.Size == uSize);

                if(dbFile == null)
                {
                    if(_onlyKnown)
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

                if(!knownFile)
                    _pendingFiles[checksums[ChecksumType.Sha512]] = dbFile;

                byte[] sha384Bytes = new byte[48];
                string sha384      = checksums[ChecksumType.Sha384];

                for(int i = 0; i < 48; i++)
                {
                    if(sha384[i * 2] >= 0x30 &&
                       sha384[i * 2] <= 0x39)
                        sha384Bytes[i] = (byte)((sha384[i * 2] - 0x30) * 0x10);
                    else if(sha384[i * 2] >= 0x41 &&
                            sha384[i * 2] <= 0x46)
                        sha384Bytes[i] = (byte)((sha384[i * 2] - 0x37) * 0x10);
                    else if(sha384[i * 2] >= 0x61 &&
                            sha384[i * 2] <= 0x66)
                        sha384Bytes[i] = (byte)((sha384[i * 2] - 0x57) * 0x10);

                    if(sha384[(i * 2) + 1] >= 0x30 &&
                       sha384[(i * 2) + 1] <= 0x39)
                        sha384Bytes[i] += (byte)(sha384[(i * 2) + 1] - 0x30);
                    else if(sha384[(i * 2) + 1] >= 0x41 &&
                            sha384[(i * 2) + 1] <= 0x46)
                        sha384Bytes[i] += (byte)(sha384[(i * 2) + 1] - 0x37);
                    else if(sha384[(i * 2) + 1] >= 0x61 &&
                            sha384[(i * 2) + 1] <= 0x66)
                        sha384Bytes[i] += (byte)(sha384[(i * 2) + 1] - 0x57);
                }

                string sha384B32 = Base32.ToBase32String(sha384Bytes);

                string repoPath = Path.Combine(Settings.Settings.Current.RepositoryPath, "files",
                                               sha384B32[0].ToString(), sha384B32[1].ToString(),
                                               sha384B32[2].ToString(), sha384B32[3].ToString(),
                                               sha384B32[4].ToString());

                if(!Directory.Exists(repoPath))
                    Directory.CreateDirectory(repoPath);

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

                    if(!fileInDb)
                        _newFiles.Add(dbFile);

                    inFs.Close();

                    if(_deleteAfterImport)
                        File.Delete(path);

                    return true;
                }

                inFs.Position = 0;

                var    outFs   = new FileStream(repoPath, FileMode.CreateNew, FileAccess.Write);
                Stream zStream = null;
                zStream = new LZipStream(outFs, CompressionMode.Compress);

                SetProgressBounds2?.Invoke(this, new ProgressBoundsEventArgs
                {
                    Minimum = 0,
                    Maximum = inFs.Length
                });

                SetMessage2?.Invoke(this, new MessageEventArgs
                {
                    Message = Localization.CompressingFile
                });

                buffer = new byte[BUFFER_SIZE];

                while(inFs.Position + BUFFER_SIZE <= inFs.Length)
                {
                    SetProgress2?.Invoke(this, new ProgressEventArgs
                    {
                        Value = inFs.Position
                    });

                    inFs.Read(buffer, 0, buffer.Length);
                    zStream.Write(buffer, 0, buffer.Length);
                }

                buffer = new byte[inFs.Length - inFs.Position];

                SetProgress2?.Invoke(this, new ProgressEventArgs
                {
                    Value = inFs.Position
                });

                inFs.Read(buffer, 0, buffer.Length);
                zStream.Write(buffer, 0, buffer.Length);

                SetIndeterminateProgress2?.Invoke(this, System.EventArgs.Empty);

                SetMessage2?.Invoke(this, new MessageEventArgs
                {
                    Message = Localization.Finishing
                });

                inFs.Close();
                zStream.Close();
                outFs.Dispose();

                dbFile.IsInRepo  = true;
                dbFile.UpdatedOn = DateTime.UtcNow;

                if(!fileInDb)
                    _newFiles.Add(dbFile);

                if(_deleteAfterImport)
                    File.Delete(path);

                return true;
            }
            catch(Exception e)
            {
                _lastMessage = Localization.UnhandledExceptionWhenImporting;

                return false;
            }
        }

        bool ImportDisk(string path)
        {
            try
            {
                var inFs = new FileStream(path, FileMode.Open, FileAccess.Read);

                SetMessage2?.Invoke(this, new MessageEventArgs
                {
                    Message = Localization.HashingFile
                });

                var chd = CHDFile.Create(path);

                if(chd == null)
                {
                    _lastMessage = Localization.NotAChdFile;

                    return false;
                }

                if(chd.MD5  == null &&
                   chd.SHA1 == null)
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

                        chdArray[i * 2]       = (char)nibble1;
                        chdArray[(i * 2) + 1] = (char)nibble2;
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

                        nibble1 += nibble1 >= 0xA ? 0x37 : 0x30;
                        nibble2 += nibble2 >= 0xA ? 0x37 : 0x30;

                        chdArray[i * 2]       = (char)nibble1;
                        chdArray[(i * 2) + 1] = (char)nibble2;
                    }

                    sha1 = new string(chdArray);
                }

                ulong  uSize              = (ulong)inFs.Length;
                bool   diskInDb           = true;
                DbDisk dbDisk             = null;
                bool   knownDisk          = false;
                bool   knownDiskWasBigger = false;

                if(sha1 != null)
                    knownDisk = _pendingDisksBySha1.TryGetValue(sha1, out dbDisk);

                if(!knownDisk &&
                   md5 != null)
                    knownDisk = _pendingDisksByMd5.TryGetValue(md5, out dbDisk);

                dbDisk ??= Context.Singleton.Disks.FirstOrDefault(d => (d.Sha1 != null && d.Sha1 == sha1) ||
                                                                       (d.Md5  != null && d.Md5  == sha1));

                if(dbDisk == null)
                {
                    if(_onlyKnown)
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
                        _pendingDisksBySha1[sha1] = dbDisk;
                    else if(md5 != null)
                        _pendingDisksByMd5[md5] = dbDisk;
                }

                string sha1B32 = null;
                string md5B32  = null;

                if(chd.SHA1 != null)
                    sha1B32 = Base32.ToBase32String(chd.SHA1);

                if(chd.MD5 != null)
                    md5B32 = Base32.ToBase32String(chd.SHA1);

                if(dbDisk.Md5 == null &&
                   md5        != null)
                {
                    dbDisk.Md5       = md5;
                    dbDisk.UpdatedOn = DateTime.UtcNow;
                }

                if(dbDisk.Sha1 == null &&
                   sha1        != null)
                {
                    dbDisk.Sha1      = sha1;
                    dbDisk.UpdatedOn = DateTime.UtcNow;
                }

                if(dbDisk.Size == null ||
                   dbDisk.Size > uSize)
                {
                    dbDisk.Size        = uSize;
                    dbDisk.UpdatedOn   = DateTime.UtcNow;
                    knownDiskWasBigger = true;
                }

                string md5Path  = null;
                string sha1Path = null;
                string repoPath = null;

                if(md5 != null)
                {
                    md5Path = Path.Combine(Settings.Settings.Current.RepositoryPath, "chd", "md5", md5B32[0].ToString(),
                                           md5B32[1].ToString(), md5B32[2].ToString(), md5B32[3].ToString(),
                                           md5B32[4].ToString());

                    repoPath = md5Path;

                    md5Path = Path.Combine(repoPath, md5B32 + ".chd");
                }

                if(sha1 != null)
                {
                    sha1Path = Path.Combine(Settings.Settings.Current.RepositoryPath, "chd", "sha1",
                                            sha1B32[0].ToString(), sha1B32[1].ToString(), sha1B32[2].ToString(),
                                            sha1B32[3].ToString(), sha1B32[4].ToString());

                    repoPath = sha1Path;

                    sha1Path = Path.Combine(repoPath, sha1B32 + ".chd");
                }

                if(!Directory.Exists(repoPath))
                    Directory.CreateDirectory(repoPath);

                if(File.Exists(md5Path) &&
                   sha1Path != null)
                    File.Move(md5Path, sha1Path);

                if(sha1Path != null)
                    repoPath = sha1Path;
                else if(md5Path != null)
                    repoPath = md5Path;

                if(File.Exists(repoPath))
                {
                    if(!knownDiskWasBigger)
                        File.Move(repoPath, repoPath + ".bak", true);
                    else
                    {
                        dbDisk.IsInRepo  = true;
                        dbDisk.UpdatedOn = DateTime.UtcNow;

                        if(!diskInDb)
                            _newDisks.Add(dbDisk);

                        inFs.Close();

                        if(_deleteAfterImport)
                            File.Delete(path);

                        return true;
                    }
                }

                inFs.Position = 0;
                var outFs = new FileStream(repoPath, FileMode.CreateNew, FileAccess.Write);

                SetProgressBounds2?.Invoke(this, new ProgressBoundsEventArgs
                {
                    Minimum = 0,
                    Maximum = inFs.Length
                });

                SetMessage2?.Invoke(this, new MessageEventArgs
                {
                    Message = Localization.CompressingFile
                });

                byte[] buffer = new byte[BUFFER_SIZE];

                while(inFs.Position + BUFFER_SIZE <= inFs.Length)
                {
                    SetProgress2?.Invoke(this, new ProgressEventArgs
                    {
                        Value = inFs.Position
                    });

                    inFs.Read(buffer, 0, buffer.Length);
                    outFs.Write(buffer, 0, buffer.Length);
                }

                buffer = new byte[inFs.Length - inFs.Position];

                SetProgress2?.Invoke(this, new ProgressEventArgs
                {
                    Value = inFs.Position
                });

                inFs.Read(buffer, 0, buffer.Length);
                outFs.Write(buffer, 0, buffer.Length);

                SetIndeterminateProgress2?.Invoke(this, System.EventArgs.Empty);

                SetMessage2?.Invoke(this, new MessageEventArgs
                {
                    Message = Localization.Finishing
                });

                inFs.Close();
                outFs.Close();

                dbDisk.IsInRepo  = true;
                dbDisk.UpdatedOn = DateTime.UtcNow;

                if(!diskInDb)
                    _newDisks.Add(dbDisk);

                if(_deleteAfterImport)
                    File.Delete(path);

                if(!knownDiskWasBigger)
                    File.Delete(repoPath + ".bak");

                return true;
            }
            catch(Exception e)
            {
                _lastMessage = Localization.UnhandledExceptionWhenImporting;

                return false;
            }
        }

        void SaveChanges()
        {
            SetIndeterminateProgress2?.Invoke(this, System.EventArgs.Empty);

            SetMessage2?.Invoke(this, new MessageEventArgs
            {
                Message = Localization.SavingChangesToDatabase
            });

            Context.Singleton.Files.AddRange(_newFiles);
            Context.Singleton.Disks.AddRange(_newDisks);
            Context.Singleton.SaveChanges();

            _newFiles.Clear();
            _newDisks.Clear();
        }

        string GetArchiveFormat(string path, out long counter)
        {
            counter = 0;

            if(!File.Exists(path))
                return null;

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
                string format   = null;
                var    jsReader = new JsonTextReader(new StringReader(lsarOutput));

                while(jsReader.Read())
                    switch(jsReader.TokenType)
                    {
                        case JsonToken.PropertyName
                            when jsReader.Value != null && jsReader.Value.ToString() == "XADFileName":
                            counter++;

                            break;
                        case JsonToken.PropertyName
                            when jsReader.Value != null && jsReader.Value.ToString() == "lsarFormatName":
                            jsReader.Read();

                            if(jsReader.TokenType == JsonToken.String &&
                               jsReader.Value     != null)
                                format = jsReader.Value.ToString();

                            break;
                    }

                return counter == 0 ? null : format;
            }
            catch(Exception)
            {
                return null;
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

            unarProcess.OutputDataReceived += (sender, e) =>
            {
                counter++;

                SetMessage2?.Invoke(this, new MessageEventArgs
                {
                    Message = e.Data
                });

                SetProgress2?.Invoke(this, new ProgressEventArgs
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
}