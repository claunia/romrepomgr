using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using RomRepoMgr.Core.EventArgs;
using RomRepoMgr.Core.Models;
using RomRepoMgr.Database;
using RomRepoMgr.Database.Models;
using SharpCompress.Compressors;
using SharpCompress.Compressors.LZMA;

namespace RomRepoMgr.Core.Workers
{
    public class FileImporter
    {
        const    long BUFFER_SIZE = 131072;
        readonly bool _deleteAfterImport;
        readonly bool _onlyKnown;

        readonly Dictionary<string, DbFile> _pendingFiles;

        string _lastMessage;
        long   _position;
        long   _totalFiles;

        public FileImporter(bool onlyKnown, bool deleteAfterImport)
        {
            _pendingFiles      = new Dictionary<string, DbFile>();
            _onlyKnown         = onlyKnown;
            _deleteAfterImport = deleteAfterImport;
            _position          = 0;
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

        public void ProcessPath(string path, bool rootPath, bool removePathOnFinish)
        {
            try
            {
                SetIndeterminateProgress?.Invoke(this, System.EventArgs.Empty);

                SetMessage?.Invoke(this, new MessageEventArgs
                {
                    Message = "Enumerating files..."
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
                    SetProgress?.Invoke(this, new ProgressEventArgs
                    {
                        Value = _position
                    });

                    SetMessage?.Invoke(this, new MessageEventArgs
                    {
                        Message = string.Format("Importing {0}...", Path.GetFileName(file))
                    });

                    bool ret = ImportRom(file);

                    if(ret)
                    {
                        ImportedRom?.Invoke(this, new ImportedRomItemEventArgs
                        {
                            Item = new ImportRomItem
                            {
                                Filename = Path.GetFileName(file),
                                Status   = "OK"
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
                                Status   = string.Format("Error: {0}", _lastMessage)
                            }
                        });
                    }

                    _position++;
                }

                if(removePathOnFinish)
                {
                    SetMessage?.Invoke(this, new MessageEventArgs
                    {
                        Message = "Removing temporary path..."
                    });

                    Directory.Delete(path, true);
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

                byte[] dataBuffer;

                SetMessage2?.Invoke(this, new MessageEventArgs
                {
                    Message = "Hashing file..."
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

                        dataBuffer = new byte[BUFFER_SIZE];
                        inFs.Read(dataBuffer, 0, (int)BUFFER_SIZE);
                        checksumWorker.Update(dataBuffer);
                    }

                    SetProgress2?.Invoke(this, new ProgressEventArgs
                    {
                        Value = offset
                    });

                    dataBuffer = new byte[remainder];
                    inFs.Read(dataBuffer, 0, (int)remainder);
                    checksumWorker.Update(dataBuffer);
                }
                else
                {
                    SetIndeterminateProgress2?.Invoke(this, System.EventArgs.Empty);
                    dataBuffer = new byte[inFs.Length];
                    inFs.Read(dataBuffer, 0, (int)inFs.Length);
                    checksumWorker.Update(dataBuffer);
                }

                Dictionary<ChecksumType, string> checksums = checksumWorker.End();

                ulong uSize    = (ulong)inFs.Length;
                bool  fileInDb = true;

                bool knownFile = _pendingFiles.TryGetValue(checksums[ChecksumType.Sha512], out DbFile dbFile);

                dbFile ??=
                    ((((Context.Singleton.Files.FirstOrDefault(f => f.Size   == uSize &&
                                                                    f.Sha512 == checksums[ChecksumType.Sha512]) ??
                        Context.Singleton.Files.FirstOrDefault(f => f.Size   == uSize &&
                                                                    f.Sha384 == checksums[ChecksumType.Sha384])) ??
                       Context.Singleton.Files.FirstOrDefault(f => f.Size   == uSize &&
                                                                   f.Sha256 == checksums[ChecksumType.Sha256])) ??
                      Context.Singleton.Files.FirstOrDefault(f => f.Size == uSize &&
                                                                  f.Sha1 == checksums[ChecksumType.Sha1])) ??
                     Context.Singleton.Files.FirstOrDefault(f => f.Size == uSize &&
                                                                 f.Md5  == checksums[ChecksumType.Md5])) ??
                    Context.Singleton.Files.FirstOrDefault(f => f.Size  == uSize &&
                                                                f.Crc32 == checksums[ChecksumType.Crc32]);

                if(dbFile == null)
                {
                    if(_onlyKnown)
                    {
                        _lastMessage = "Unknown file.";

                        return false;
                    }

                    dbFile = new DbFile
                    {
                        Crc32     = checksums[ChecksumType.Crc32],
                        Md5       = checksums[ChecksumType.Md5],
                        Sha1      = checksums[ChecksumType.Sha1],
                        Sha256    = checksums[ChecksumType.Sha256],
                        Sha384    = checksums[ChecksumType.Sha384],
                        Sha512    = checksums[ChecksumType.Sha512],
                        Size      = uSize,
                        CreatedOn = DateTime.UtcNow,
                        UpdatedOn = DateTime.UtcNow
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
                        Context.Singleton.Files.Add(dbFile);

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
                    Message = "Compressing file..."
                });

                byte[] buffer = new byte[BUFFER_SIZE];

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
                    Message = "Finishing..."
                });

                inFs.Close();
                zStream.Close();
                outFs.Dispose();

                dbFile.IsInRepo  = true;
                dbFile.UpdatedOn = DateTime.UtcNow;

                if(!fileInDb)
                    Context.Singleton.Files.Add(dbFile);

                if(_deleteAfterImport)
                    File.Delete(path);

                return true;
            }
            catch(Exception e)
            {
                _lastMessage = "Unhandled exception when importing file.";

                return false;
            }
        }

        void SaveChanges()
        {
            SetIndeterminateProgress2?.Invoke(this, System.EventArgs.Empty);

            SetMessage2?.Invoke(this, new MessageEventArgs
            {
                Message = "Saving changes to database..."
            });

            Context.Singleton.SaveChanges();
        }
    }
}