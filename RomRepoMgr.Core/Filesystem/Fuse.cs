using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Mono.Fuse.NETStandard;
using Mono.Unix.Native;
using RomRepoMgr.Database;
using RomRepoMgr.Database.Models;
using SharpCompress.Compressors;
using SharpCompress.Compressors.LZMA;

namespace RomRepoMgr.Core.Filesystem
{
    // TODO: Invalidate caches
    // TODO: Mount options
    // TODO: Do not show machines or romsets with no ROMs in repo
    // TODO: Last handle goes negative
    public sealed class Fuse : FileSystem
    {
        readonly ConcurrentDictionary<long, List<DirectoryEntry>>                        _directoryCache;
        readonly ConcurrentDictionary<long, Stat>                                        _fileStatHandleCache;
        readonly ConcurrentDictionary<ulong, ConcurrentDictionary<string, CachedFile>>   _machineFilesCache;
        readonly ConcurrentDictionary<long, ConcurrentDictionary<string, CachedMachine>> _machinesStatCache;
        readonly ConcurrentDictionary<long, RomSet>                                      _romSetsCache;
        readonly ConcurrentDictionary<long, Stream>                                      _streamsCache;
        long                                                                             _lastHandle;
        ConcurrentDictionary<string, long>                                               _rootDirectoryCache;

        public Fuse()
        {
            _directoryCache      = new ConcurrentDictionary<long, List<DirectoryEntry>>();
            _lastHandle          = 0;
            _rootDirectoryCache  = new ConcurrentDictionary<string, long>();
            _machinesStatCache   = new ConcurrentDictionary<long, ConcurrentDictionary<string, CachedMachine>>();
            _romSetsCache        = new ConcurrentDictionary<long, RomSet>();
            _machineFilesCache   = new ConcurrentDictionary<ulong, ConcurrentDictionary<string, CachedFile>>();
            _streamsCache        = new ConcurrentDictionary<long, Stream>();
            _fileStatHandleCache = new ConcurrentDictionary<long, Stat>();
            Name                 = "romrepombgrfs";
        }

        protected override void Dispose(bool disposing)
        {
            if(!disposing)
                return;

            // TODO: Close streams manually
        }

        protected override Errno OnGetPathStatus(string path, out Stat stat)
        {
            stat = new Stat();

            string[] pieces = path.Split("/", StringSplitOptions.RemoveEmptyEntries);

            if(pieces.Length == 0)
            {
                stat.st_mode  = FilePermissions.S_IFDIR | NativeConvert.FromOctalPermissionString("0555");
                stat.st_nlink = 2;

                return 0;
            }

            if(_rootDirectoryCache.Count == 0)
                FillRootDirectoryCache();

            if(!_rootDirectoryCache.TryGetValue(pieces[0], out long romSetId))
                return Errno.ENOENT;

            if(!_romSetsCache.TryGetValue(romSetId, out RomSet romSet))
            {
                using var ctx = Context.Create(Settings.Settings.Current.DatabasePath);

                romSet                  = ctx.RomSets.Find(romSetId);
                _romSetsCache[romSetId] = romSet;
            }

            if(romSet == null)
                return Errno.ENOENT;

            if(pieces.Length == 1)
            {
                stat.st_mode  = FilePermissions.S_IFDIR | NativeConvert.FromOctalPermissionString("0555");
                stat.st_nlink = 2;
                stat.st_ctime = NativeConvert.ToTimeT(romSet.CreatedOn.ToUniversalTime());
                stat.st_mtime = NativeConvert.ToTimeT(romSet.UpdatedOn.ToUniversalTime());

                return 0;
            }

            _machinesStatCache.TryGetValue(romSetId, out ConcurrentDictionary<string, CachedMachine> cachedMachines);

            if(cachedMachines == null)
            {
                cachedMachines = new ConcurrentDictionary<string, CachedMachine>();

                using var ctx = Context.Create(Settings.Settings.Current.DatabasePath);

                foreach(Machine mach in ctx.Machines.Where(m => m.RomSet.Id == romSetId))
                {
                    cachedMachines[mach.Name] = new CachedMachine
                    {
                        Id = mach.Id,
                        Stat = new Stat
                        {
                            st_mode  = FilePermissions.S_IFDIR | NativeConvert.FromOctalPermissionString("0555"),
                            st_nlink = 2,
                            st_ctime = NativeConvert.ToTimeT(mach.CreatedOn.ToUniversalTime()),
                            st_mtime = NativeConvert.ToTimeT(mach.UpdatedOn.ToUniversalTime())
                        }
                    };
                }

                _machinesStatCache[romSetId] = cachedMachines;
            }

            if(!cachedMachines.TryGetValue(pieces[1], out CachedMachine machineStat))
                return Errno.ENOENT;

            if(pieces.Length == 2)
            {
                stat = machineStat.Stat;

                return 0;
            }

            _machineFilesCache.TryGetValue(machineStat.Id,
                                           out ConcurrentDictionary<string, CachedFile> cachedMachineFiles);

            if(cachedMachineFiles == null)
            {
                using var ctx = Context.Create(Settings.Settings.Current.DatabasePath);

                cachedMachineFiles = new ConcurrentDictionary<string, CachedFile>();

                foreach(FileByMachine machineFile in
                    ctx.FilesByMachines.Where(fbm => fbm.Machine.Id == machineStat.Id && fbm.File.IsInRepo))
                {
                    var cachedFile = new CachedFile
                    {
                        Id        = machineFile.File.Id,
                        Crc32     = machineFile.File.Crc32,
                        Md5       = machineFile.File.Md5,
                        Sha1      = machineFile.File.Sha1,
                        Sha256    = machineFile.File.Sha256,
                        Sha384    = machineFile.File.Sha384,
                        Sha512    = machineFile.File.Sha512,
                        Size      = machineFile.File.Size,
                        CreatedOn = machineFile.File.CreatedOn,
                        UpdatedOn = machineFile.File.UpdatedOn
                    };

                    cachedMachineFiles[machineFile.Name] = cachedFile;
                }

                _machineFilesCache[machineStat.Id] = cachedMachineFiles;
            }

            if(!cachedMachineFiles.TryGetValue(pieces[2], out CachedFile file))
                return Errno.ENOENT;

            if(pieces.Length == 3)
            {
                stat = new Stat
                {
                    st_mode    = FilePermissions.S_IFREG | NativeConvert.FromOctalPermissionString("0444"),
                    st_nlink   = 1,
                    st_ctime   = NativeConvert.ToTimeT(file.CreatedOn.ToUniversalTime()),
                    st_mtime   = NativeConvert.ToTimeT(file.UpdatedOn.ToUniversalTime()),
                    st_blksize = 512,
                    st_blocks  = (long)(file.Size / 512),
                    st_ino     = file.Id,
                    st_size    = (long)file.Size
                };

                return 0;
            }

            return Errno.ENOSYS;
        }

        protected override Errno OnReadSymbolicLink(string link, out string target)
        {
            target = null;

            return Errno.EOPNOTSUPP;
        }

        protected override Errno OnCreateSpecialFile(string file, FilePermissions perms, ulong dev) => Errno.EROFS;

        protected override Errno OnCreateDirectory(string directory, FilePermissions mode) => Errno.EROFS;

        protected override Errno OnRemoveFile(string file) => Errno.EROFS;

        protected override Errno OnRemoveDirectory(string directory) => Errno.EROFS;

        protected override Errno OnCreateSymbolicLink(string target, string link) => Errno.EROFS;

        protected override Errno OnRenamePath(string oldPath, string newPath) => Errno.EROFS;

        protected override Errno OnCreateHardLink(string oldPath, string link) => Errno.EROFS;

        protected override Errno OnChangePathPermissions(string path, FilePermissions mode) => Errno.EROFS;

        protected override Errno OnChangePathOwner(string path, long owner, long group) => Errno.EROFS;

        protected override Errno OnTruncateFile(string file, long length) => Errno.EROFS;

        protected override Errno OnChangePathTimes(string path, ref Utimbuf buf) => Errno.EROFS;

        protected override Errno OnOpenHandle(string path, OpenedPathInfo info)
        {
            string[] pieces = path.Split("/", StringSplitOptions.RemoveEmptyEntries);

            if(pieces.Length == 0)
                return Errno.EISDIR;

            if(!_rootDirectoryCache.TryGetValue(pieces[0], out long romSetId))
                return Errno.ENOENT;

            if(!_romSetsCache.TryGetValue(romSetId, out RomSet romSet))
            {
                using var ctx = Context.Create(Settings.Settings.Current.DatabasePath);

                romSet                  = ctx.RomSets.Find(romSetId);
                _romSetsCache[romSetId] = romSet;
            }

            if(romSet == null)
                return Errno.ENOENT;

            if(pieces.Length == 1)
                return Errno.EISDIR;

            _machinesStatCache.TryGetValue(romSetId, out ConcurrentDictionary<string, CachedMachine> cachedMachines);

            if(cachedMachines == null)
            {
                cachedMachines = new ConcurrentDictionary<string, CachedMachine>();

                using var ctx = Context.Create(Settings.Settings.Current.DatabasePath);

                foreach(Machine mach in ctx.Machines.Where(m => m.RomSet.Id == romSetId))
                {
                    cachedMachines[mach.Name] = new CachedMachine
                    {
                        Id = mach.Id,
                        Stat = new Stat
                        {
                            st_mode  = FilePermissions.S_IFDIR | NativeConvert.FromOctalPermissionString("0555"),
                            st_nlink = 2,
                            st_ctime = NativeConvert.ToTimeT(mach.CreatedOn.ToUniversalTime()),
                            st_mtime = NativeConvert.ToTimeT(mach.UpdatedOn.ToUniversalTime())
                        }
                    };
                }

                _machinesStatCache[romSetId] = cachedMachines;
            }

            if(!cachedMachines.TryGetValue(pieces[1], out CachedMachine machineStat))
                return Errno.ENOENT;

            if(pieces.Length == 2)
                return Errno.EISDIR;

            _machineFilesCache.TryGetValue(machineStat.Id,
                                           out ConcurrentDictionary<string, CachedFile> cachedMachineFiles);

            if(cachedMachineFiles == null)
            {
                using var ctx = Context.Create(Settings.Settings.Current.DatabasePath);

                cachedMachineFiles = new ConcurrentDictionary<string, CachedFile>();

                foreach(FileByMachine machineFile in
                    ctx.FilesByMachines.Where(fbm => fbm.Machine.Id == machineStat.Id && fbm.File.IsInRepo))
                {
                    var cachedFile = new CachedFile
                    {
                        Id        = machineFile.File.Id,
                        Crc32     = machineFile.File.Crc32,
                        Md5       = machineFile.File.Md5,
                        Sha1      = machineFile.File.Sha1,
                        Sha256    = machineFile.File.Sha256,
                        Sha384    = machineFile.File.Sha384,
                        Sha512    = machineFile.File.Sha512,
                        Size      = machineFile.File.Size,
                        CreatedOn = machineFile.File.CreatedOn,
                        UpdatedOn = machineFile.File.UpdatedOn
                    };

                    cachedMachineFiles[machineFile.Name] = cachedFile;
                }

                _machineFilesCache[machineStat.Id] = cachedMachineFiles;
            }

            if(!cachedMachineFiles.TryGetValue(pieces[2], out CachedFile file))
                return Errno.ENOENT;

            if(pieces.Length > 3)
                return Errno.ENOSYS;

            if(file.Sha384 == null)
                return Errno.ENOENT;

            if(info.OpenAccess.HasFlag(OpenFlags.O_APPEND) ||
               info.OpenAccess.HasFlag(OpenFlags.O_CREAT)  ||
               info.OpenAccess.HasFlag(OpenFlags.O_EXCL)   ||
               info.OpenAccess.HasFlag(OpenFlags.O_TRUNC))
                return Errno.EROFS;

            byte[] sha384Bytes = new byte[48];
            string sha384      = file.Sha384;

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

            string repoPath = Path.Combine(Settings.Settings.Current.RepositoryPath, "files", sha384B32[0].ToString(),
                                           sha384B32[1].ToString(), sha384B32[2].ToString(), sha384B32[3].ToString(),
                                           sha384B32[4].ToString(), sha384B32 + ".lz");

            if(!File.Exists(repoPath))
                return Errno.ENOENT;

            _lastHandle++;
            info.Handle = new IntPtr(_lastHandle);

            _streamsCache[_lastHandle] =
                Stream.Synchronized(new ForcedSeekStream<LZipStream>((long)file.Size,
                                                                     new FileStream(repoPath, FileMode.Open,
                                                                         FileAccess.Read),
                                                                     CompressionMode.Decompress));

            _fileStatHandleCache[_lastHandle] = new Stat
            {
                st_mode    = FilePermissions.S_IFREG | NativeConvert.FromOctalPermissionString("0444"),
                st_nlink   = 1,
                st_ctime   = NativeConvert.ToTimeT(file.CreatedOn.ToUniversalTime()),
                st_mtime   = NativeConvert.ToTimeT(file.UpdatedOn.ToUniversalTime()),
                st_blksize = 512,
                st_blocks  = (long)(file.Size / 512),
                st_ino     = file.Id,
                st_size    = (long)file.Size
            };

            return 0;
        }

        protected override Errno OnReadHandle(string file, OpenedPathInfo info, byte[] buf, long offset,
                                              out int bytesWritten)
        {
            bytesWritten = 0;

            if(!_streamsCache.TryGetValue(info.Handle.ToInt64(), out Stream fileStream))
                return Errno.EBADF;

            fileStream.Position = offset;
            bytesWritten        = fileStream.Read(buf, 0, buf.Length);

            return 0;
        }

        protected override Errno OnWriteHandle(string file, OpenedPathInfo info, byte[] buf, long offset,
                                               out int bytesRead)
        {
            bytesRead = 0;

            return Errno.EROFS;
        }

        protected override Errno OnGetFileSystemStatus(string path, out Statvfs buf)
        {
            using var ctx = Context.Create(Settings.Settings.Current.DatabasePath);

            buf = new Statvfs
            {
                f_bsize   = 512,
                f_frsize  = 512,
                f_blocks  = (ulong)(ctx.Files.Where(f => f.IsInRepo).Sum(f => (double)f.Size) / 512),
                f_bavail  = 0,
                f_files   = (ulong)ctx.Files.Count(f => f.IsInRepo),
                f_ffree   = 0,
                f_favail  = 0,
                f_fsid    = 0xFFFFFFFF,
                f_flag    = 0,
                f_namemax = 255
            };

            return 0;
        }

        protected override Errno OnFlushHandle(string file, OpenedPathInfo info) => Errno.ENOSYS;

        protected override Errno OnReleaseHandle(string file, OpenedPathInfo info)
        {
            if(!_streamsCache.TryGetValue(info.Handle.ToInt64(), out Stream fileStream))
                return Errno.EBADF;

            fileStream.Close();
            _streamsCache.TryRemove(info.Handle.ToInt64(), out _);
            _fileStatHandleCache.TryRemove(info.Handle.ToInt64(), out _);

            return 0;
        }

        protected override Errno OnSynchronizeHandle(string file, OpenedPathInfo info, bool onlyUserData) =>
            Errno.EOPNOTSUPP;

        protected override Errno OnSetPathExtendedAttribute(string path, string name, byte[] value, XattrFlags flags) =>
            Errno.EROFS;

        protected override Errno OnGetPathExtendedAttribute(string path, string name, byte[] value,
                                                            out int bytesWritten)
        {
            bytesWritten = 0;

            string[] pieces = path.Split("/", StringSplitOptions.RemoveEmptyEntries);

            if(pieces.Length == 0)
                return Errno.ENODATA;

            if(!_rootDirectoryCache.TryGetValue(pieces[0], out long romSetId))
                return Errno.ENOENT;

            if(!_romSetsCache.TryGetValue(romSetId, out RomSet romSet))
            {
                using var ctx = Context.Create(Settings.Settings.Current.DatabasePath);

                romSet                  = ctx.RomSets.Find(romSetId);
                _romSetsCache[romSetId] = romSet;
            }

            if(romSet == null)
                return Errno.ENOENT;

            if(pieces.Length == 1)
                return Errno.ENODATA;

            _machinesStatCache.TryGetValue(romSetId, out ConcurrentDictionary<string, CachedMachine> cachedMachines);

            if(cachedMachines == null)
            {
                cachedMachines = new ConcurrentDictionary<string, CachedMachine>();

                using var ctx = Context.Create(Settings.Settings.Current.DatabasePath);

                foreach(Machine mach in ctx.Machines.Where(m => m.RomSet.Id == romSetId))
                {
                    cachedMachines[mach.Name] = new CachedMachine
                    {
                        Id = mach.Id,
                        Stat = new Stat
                        {
                            st_mode  = FilePermissions.S_IFDIR | NativeConvert.FromOctalPermissionString("0555"),
                            st_nlink = 2,
                            st_ctime = NativeConvert.ToTimeT(mach.CreatedOn.ToUniversalTime()),
                            st_mtime = NativeConvert.ToTimeT(mach.UpdatedOn.ToUniversalTime())
                        }
                    };
                }

                _machinesStatCache[romSetId] = cachedMachines;
            }

            if(!cachedMachines.TryGetValue(pieces[1], out CachedMachine machineStat))
                return Errno.ENOENT;

            if(pieces.Length == 2)
                return Errno.ENODATA;

            _machineFilesCache.TryGetValue(machineStat.Id,
                                           out ConcurrentDictionary<string, CachedFile> cachedMachineFiles);

            if(cachedMachineFiles == null)
            {
                using var ctx = Context.Create(Settings.Settings.Current.DatabasePath);

                cachedMachineFiles = new ConcurrentDictionary<string, CachedFile>();

                foreach(FileByMachine machineFile in
                    ctx.FilesByMachines.Where(fbm => fbm.Machine.Id == machineStat.Id && fbm.File.IsInRepo))
                {
                    var cachedFile = new CachedFile
                    {
                        Id        = machineFile.File.Id,
                        Crc32     = machineFile.File.Crc32,
                        Md5       = machineFile.File.Md5,
                        Sha1      = machineFile.File.Sha1,
                        Sha256    = machineFile.File.Sha256,
                        Sha384    = machineFile.File.Sha384,
                        Sha512    = machineFile.File.Sha512,
                        Size      = machineFile.File.Size,
                        CreatedOn = machineFile.File.CreatedOn,
                        UpdatedOn = machineFile.File.UpdatedOn
                    };

                    cachedMachineFiles[machineFile.Name] = cachedFile;
                }

                _machineFilesCache[machineStat.Id] = cachedMachineFiles;
            }

            if(!cachedMachineFiles.TryGetValue(pieces[2], out CachedFile file))
                return Errno.ENOENT;

            if(pieces.Length > 3)
                return Errno.ENOSYS;

            string hash = null;

            switch(name)
            {
                case "user.crc32":
                    hash = file.Crc32;

                    break;
                case "user.md5":
                    hash = file.Md5;

                    break;
                case "user.sha1":
                    hash = file.Sha1;

                    break;
                case "user.sha256":
                    hash = file.Sha256;

                    break;
                case "user.sha384":
                    hash = file.Sha384;

                    break;
                case "user.sha512":
                    hash = file.Sha512;

                    break;
            }

            if(hash == null)
                return Errno.ENODATA;

            byte[] xattr = new byte[hash.Length / 2];

            for(int i = 0; i < xattr.Length; i++)
            {
                if(hash[i * 2] >= 0x30 &&
                   hash[i * 2] <= 0x39)
                    xattr[i] = (byte)((hash[i * 2] - 0x30) * 0x10);
                else if(hash[i * 2] >= 0x41 &&
                        hash[i * 2] <= 0x46)
                    xattr[i] = (byte)((hash[i * 2] - 0x37) * 0x10);
                else if(hash[i * 2] >= 0x61 &&
                        hash[i * 2] <= 0x66)
                    xattr[i] = (byte)((hash[i * 2] - 0x57) * 0x10);

                if(hash[(i * 2) + 1] >= 0x30 &&
                   hash[(i * 2) + 1] <= 0x39)
                    xattr[i] += (byte)(hash[(i * 2) + 1] - 0x30);
                else if(hash[(i * 2) + 1] >= 0x41 &&
                        hash[(i * 2) + 1] <= 0x46)
                    xattr[i] += (byte)(hash[(i * 2) + 1] - 0x37);
                else if(hash[(i * 2) + 1] >= 0x61 &&
                        hash[(i * 2) + 1] <= 0x66)
                    xattr[i] += (byte)(hash[(i * 2) + 1] - 0x57);
            }

            if(value == null)
            {
                bytesWritten = xattr.Length;

                return 0;
            }

            int maxSize = value.Length > xattr.Length ? xattr.Length : value.Length;

            Array.Copy(xattr, 0, value, 0, maxSize);
            bytesWritten = maxSize;

            return 0;
        }

        protected override Errno OnListPathExtendedAttributes(string path, out string[] names)
        {
            names = null;

            string[] pieces = path.Split("/", StringSplitOptions.RemoveEmptyEntries);

            if(pieces.Length == 0)
                return 0;

            if(!_rootDirectoryCache.TryGetValue(pieces[0], out long romSetId))
                return Errno.ENOENT;

            if(!_romSetsCache.TryGetValue(romSetId, out RomSet romSet))
            {
                using var ctx = Context.Create(Settings.Settings.Current.DatabasePath);

                romSet                  = ctx.RomSets.Find(romSetId);
                _romSetsCache[romSetId] = romSet;
            }

            if(romSet == null)
                return Errno.ENOENT;

            if(pieces.Length == 1)
                return 0;

            _machinesStatCache.TryGetValue(romSetId, out ConcurrentDictionary<string, CachedMachine> cachedMachines);

            if(cachedMachines == null)
            {
                cachedMachines = new ConcurrentDictionary<string, CachedMachine>();

                using var ctx = Context.Create(Settings.Settings.Current.DatabasePath);

                foreach(Machine mach in ctx.Machines.Where(m => m.RomSet.Id == romSetId))
                {
                    cachedMachines[mach.Name] = new CachedMachine
                    {
                        Id = mach.Id,
                        Stat = new Stat
                        {
                            st_mode  = FilePermissions.S_IFDIR | NativeConvert.FromOctalPermissionString("0555"),
                            st_nlink = 2,
                            st_ctime = NativeConvert.ToTimeT(mach.CreatedOn.ToUniversalTime()),
                            st_mtime = NativeConvert.ToTimeT(mach.UpdatedOn.ToUniversalTime())
                        }
                    };
                }

                _machinesStatCache[romSetId] = cachedMachines;
            }

            if(!cachedMachines.TryGetValue(pieces[1], out CachedMachine machineStat))
                return Errno.ENOENT;

            if(pieces.Length == 2)
                return 0;

            _machineFilesCache.TryGetValue(machineStat.Id,
                                           out ConcurrentDictionary<string, CachedFile> cachedMachineFiles);

            if(cachedMachineFiles == null)
            {
                using var ctx = Context.Create(Settings.Settings.Current.DatabasePath);

                cachedMachineFiles = new ConcurrentDictionary<string, CachedFile>();

                foreach(FileByMachine machineFile in
                    ctx.FilesByMachines.Where(fbm => fbm.Machine.Id == machineStat.Id && fbm.File.IsInRepo))
                {
                    var cachedFile = new CachedFile
                    {
                        Id        = machineFile.File.Id,
                        Crc32     = machineFile.File.Crc32,
                        Md5       = machineFile.File.Md5,
                        Sha1      = machineFile.File.Sha1,
                        Sha256    = machineFile.File.Sha256,
                        Sha384    = machineFile.File.Sha384,
                        Sha512    = machineFile.File.Sha512,
                        Size      = machineFile.File.Size,
                        CreatedOn = machineFile.File.CreatedOn,
                        UpdatedOn = machineFile.File.UpdatedOn
                    };

                    cachedMachineFiles[machineFile.Name] = cachedFile;
                }

                _machineFilesCache[machineStat.Id] = cachedMachineFiles;
            }

            if(!cachedMachineFiles.TryGetValue(pieces[2], out CachedFile file))
                return Errno.ENOENT;

            if(pieces.Length > 3)
                return Errno.ENOSYS;

            List<string> xattrs = new List<string>();

            if(file.Crc32 != null)
                xattrs.Add("user.crc32");

            if(file.Md5 != null)
                xattrs.Add("user.md5");

            if(file.Sha1 != null)
                xattrs.Add("user.sha1");

            if(file.Sha256 != null)
                xattrs.Add("user.sha256");

            if(file.Sha384 != null)
                xattrs.Add("user.sha384");

            if(file.Sha512 != null)
                xattrs.Add("user.sha512");

            names = xattrs.ToArray();

            return 0;
        }

        protected override Errno OnRemovePathExtendedAttribute(string path, string name) => Errno.EROFS;

        void FillRootDirectoryCache()
        {
            using var ctx = Context.Create(Settings.Settings.Current.DatabasePath);

            List<DirectoryEntry> entries = new List<DirectoryEntry>
            {
                new DirectoryEntry("."),
                new DirectoryEntry("..")
            };

            ConcurrentDictionary<string, long> rootCache = new ConcurrentDictionary<string, long>();

            foreach(RomSet set in ctx.RomSets)
            {
                string name = set.Name.Replace('/', '∕');

                if(entries.Any(e => e.Name == name))
                    name = Path.GetFileNameWithoutExtension(set.Filename)?.Replace('/', '∕');

                if(entries.Any(e => e.Name == name) ||
                   name == null)
                    name = Path.GetFileNameWithoutExtension(set.Sha384);

                if(name == null)
                    continue;

                entries.Add(new DirectoryEntry(name));
                rootCache[name]       = set.Id;
                _romSetsCache[set.Id] = set;
            }

            _rootDirectoryCache = rootCache;
        }

        protected override Errno OnOpenDirectory(string directory, OpenedPathInfo info)
        {
            try
            {
                if(directory == "/")
                {
                    using var ctx = Context.Create(Settings.Settings.Current.DatabasePath);

                    List<DirectoryEntry> entries = new List<DirectoryEntry>
                    {
                        new DirectoryEntry("."),
                        new DirectoryEntry("..")
                    };

                    ConcurrentDictionary<string, long> rootCache = new ConcurrentDictionary<string, long>();

                    foreach(RomSet set in ctx.RomSets)
                    {
                        string name = set.Name.Replace('/', '∕');

                        if(entries.Any(e => e.Name == name))
                            name = Path.GetFileNameWithoutExtension(set.Filename)?.Replace('/', '∕');

                        if(entries.Any(e => e.Name == name) ||
                           name == null)
                            name = Path.GetFileNameWithoutExtension(set.Sha384);

                        if(name == null)
                            continue;

                        entries.Add(new DirectoryEntry(name));
                        rootCache[name]       = set.Id;
                        _romSetsCache[set.Id] = set;
                    }

                    _lastHandle++;
                    info.Handle = new IntPtr(_lastHandle);

                    _directoryCache[_lastHandle] = entries;
                    _rootDirectoryCache          = rootCache;

                    return 0;
                }

                string[] pieces = directory.Split("/", StringSplitOptions.RemoveEmptyEntries);

                if(pieces.Length == 0)
                    return Errno.ENOENT;

                if(_rootDirectoryCache.Count == 0)
                    FillRootDirectoryCache();

                if(!_rootDirectoryCache.TryGetValue(pieces[0], out long romSetId))
                    return Errno.ENOENT;

                if(!_romSetsCache.TryGetValue(romSetId, out RomSet romSet))
                {
                    using var ctx = Context.Create(Settings.Settings.Current.DatabasePath);

                    romSet                  = ctx.RomSets.Find(romSetId);
                    _romSetsCache[romSetId] = romSet;
                }

                if(romSet == null)
                    return Errno.ENOENT;

                _machinesStatCache.TryGetValue(romSetId,
                                               out ConcurrentDictionary<string, CachedMachine> cachedMachines);

                if(cachedMachines == null)
                {
                    cachedMachines = new ConcurrentDictionary<string, CachedMachine>();

                    using var ctx = Context.Create(Settings.Settings.Current.DatabasePath);

                    foreach(Machine mach in ctx.Machines.Where(m => m.RomSet.Id == romSetId))
                    {
                        cachedMachines[mach.Name] = new CachedMachine
                        {
                            Id = mach.Id,
                            Stat = new Stat
                            {
                                st_mode  = FilePermissions.S_IFDIR | NativeConvert.FromOctalPermissionString("0755"),
                                st_nlink = 2,
                                st_ctime = NativeConvert.ToTimeT(mach.CreatedOn.ToUniversalTime()),
                                st_mtime = NativeConvert.ToTimeT(mach.UpdatedOn.ToUniversalTime())
                            }
                        };
                    }

                    _machinesStatCache[romSetId] = cachedMachines;
                }

                if(pieces.Length == 1)
                {
                    List<DirectoryEntry> entries = new List<DirectoryEntry>
                    {
                        new DirectoryEntry("."),
                        new DirectoryEntry("..")
                    };

                    entries.AddRange(cachedMachines.Select(mach => new DirectoryEntry(mach.Key)));

                    _lastHandle++;
                    info.Handle = new IntPtr(_lastHandle);

                    _directoryCache[_lastHandle] = entries;

                    return 0;
                }

                cachedMachines.TryGetValue(pieces[1], out CachedMachine machine);

                if(machine == null)
                {
                    using var ctx = Context.Create(Settings.Settings.Current.DatabasePath);

                    Machine mach = ctx.Machines.FirstOrDefault(m => m.RomSet.Id == romSetId && m.Name == pieces[1]);

                    if(mach == null)
                        return Errno.ENOENT;

                    cachedMachines[mach.Name] = new CachedMachine
                    {
                        Id = mach.Id,
                        Stat = new Stat
                        {
                            st_mode  = FilePermissions.S_IFDIR | NativeConvert.FromOctalPermissionString("0755"),
                            st_nlink = 2,
                            st_ctime = NativeConvert.ToTimeT(mach.CreatedOn.ToUniversalTime()),
                            st_mtime = NativeConvert.ToTimeT(mach.UpdatedOn.ToUniversalTime())
                        }
                    };

                    machine = cachedMachines[mach.Name];
                }

                _machineFilesCache.TryGetValue(machine.Id,
                                               out ConcurrentDictionary<string, CachedFile> cachedMachineFiles);

                if(cachedMachineFiles == null)
                {
                    using var ctx = Context.Create(Settings.Settings.Current.DatabasePath);

                    cachedMachineFiles = new ConcurrentDictionary<string, CachedFile>();

                    foreach(FileByMachine machineFile in
                        ctx.FilesByMachines.Where(fbm => fbm.Machine.Id == machine.Id && fbm.File.IsInRepo))
                    {
                        var file = new CachedFile
                        {
                            Id        = machineFile.File.Id,
                            Crc32     = machineFile.File.Crc32,
                            Md5       = machineFile.File.Md5,
                            Sha1      = machineFile.File.Sha1,
                            Sha256    = machineFile.File.Sha256,
                            Sha384    = machineFile.File.Sha384,
                            Sha512    = machineFile.File.Sha512,
                            Size      = machineFile.File.Size,
                            CreatedOn = machineFile.File.CreatedOn,
                            UpdatedOn = machineFile.File.UpdatedOn
                        };

                        cachedMachineFiles[machineFile.Name] = file;
                    }

                    _machineFilesCache[machine.Id] = cachedMachineFiles;
                }

                if(pieces.Length == 2)
                {
                    List<DirectoryEntry> entries = new List<DirectoryEntry>
                    {
                        new DirectoryEntry("."),
                        new DirectoryEntry("..")
                    };

                    entries.AddRange(cachedMachineFiles.Select(file => new DirectoryEntry(file.Key)));

                    _lastHandle++;
                    info.Handle = new IntPtr(_lastHandle);

                    _directoryCache[_lastHandle] = entries;

                    return 0;
                }

                // TODO: DATs with subfolders as game name
                if(pieces.Length >= 3)
                    return Errno.EISDIR;

                return Errno.ENOENT;
            }
            catch(Exception e)
            {
                Console.WriteLine(e);

                throw;
            }
        }

        protected override Errno OnReadDirectory(string directory, OpenedPathInfo info,
                                                 out IEnumerable<DirectoryEntry> paths)
        {
            paths = null;

            if(!_directoryCache.TryGetValue(info.Handle.ToInt64(), out List<DirectoryEntry> cache))
                return Errno.EBADF;

            paths = cache;

            return 0;
        }

        protected override Errno OnReleaseDirectory(string directory, OpenedPathInfo info)
        {
            if(!_directoryCache.TryGetValue(info.Handle.ToInt64(), out _))
                return Errno.EBADF;

            _directoryCache.Remove(info.Handle.ToInt64(), out _);

            return 0;
        }

        protected override Errno OnSynchronizeDirectory(string directory, OpenedPathInfo info, bool onlyUserData) =>
            Errno.ENOSYS;

        protected override Errno OnAccessPath(string path, AccessModes mode)
        {
            string[] pieces = path.Split("/", StringSplitOptions.RemoveEmptyEntries);

            if(pieces.Length == 0)
                return mode.HasFlag(AccessModes.W_OK) ? Errno.EROFS : 0;

            if(_rootDirectoryCache.Count == 0)
                FillRootDirectoryCache();

            if(!_rootDirectoryCache.TryGetValue(pieces[0], out long romSetId))
                return Errno.ENOENT;

            if(!_romSetsCache.TryGetValue(romSetId, out RomSet romSet))
            {
                using var ctx = Context.Create(Settings.Settings.Current.DatabasePath);

                romSet                  = ctx.RomSets.Find(romSetId);
                _romSetsCache[romSetId] = romSet;
            }

            if(romSet == null)
                return Errno.ENOENT;

            if(pieces.Length == 1)
                return mode.HasFlag(AccessModes.W_OK) ? Errno.EROFS : 0;

            _machinesStatCache.TryGetValue(romSetId, out ConcurrentDictionary<string, CachedMachine> cachedMachines);

            if(cachedMachines == null)
            {
                cachedMachines = new ConcurrentDictionary<string, CachedMachine>();

                using var ctx = Context.Create(Settings.Settings.Current.DatabasePath);

                foreach(Machine mach in ctx.Machines.Where(m => m.RomSet.Id == romSetId))
                {
                    cachedMachines[mach.Name] = new CachedMachine
                    {
                        Id = mach.Id,
                        Stat = new Stat
                        {
                            st_mode  = FilePermissions.S_IFDIR | NativeConvert.FromOctalPermissionString("0555"),
                            st_nlink = 2,
                            st_ctime = NativeConvert.ToTimeT(mach.CreatedOn.ToUniversalTime()),
                            st_mtime = NativeConvert.ToTimeT(mach.UpdatedOn.ToUniversalTime())
                        }
                    };
                }

                _machinesStatCache[romSetId] = cachedMachines;
            }

            if(!cachedMachines.TryGetValue(pieces[1], out CachedMachine machineStat))
                return Errno.ENOENT;

            if(pieces.Length == 2)
                return mode.HasFlag(AccessModes.W_OK) ? Errno.EROFS : 0;

            _machineFilesCache.TryGetValue(machineStat.Id,
                                           out ConcurrentDictionary<string, CachedFile> cachedMachineFiles);

            if(cachedMachineFiles == null)
            {
                using var ctx = Context.Create(Settings.Settings.Current.DatabasePath);

                cachedMachineFiles = new ConcurrentDictionary<string, CachedFile>();

                foreach(FileByMachine machineFile in
                    ctx.FilesByMachines.Where(fbm => fbm.Machine.Id == machineStat.Id && fbm.File.IsInRepo))
                {
                    var cachedFile = new CachedFile
                    {
                        Id        = machineFile.File.Id,
                        Crc32     = machineFile.File.Crc32,
                        Md5       = machineFile.File.Md5,
                        Sha1      = machineFile.File.Sha1,
                        Sha256    = machineFile.File.Sha256,
                        Sha384    = machineFile.File.Sha384,
                        Sha512    = machineFile.File.Sha512,
                        Size      = machineFile.File.Size,
                        CreatedOn = machineFile.File.CreatedOn,
                        UpdatedOn = machineFile.File.UpdatedOn
                    };

                    cachedMachineFiles[machineFile.Name] = cachedFile;
                }

                _machineFilesCache[machineStat.Id] = cachedMachineFiles;
            }

            if(!cachedMachineFiles.TryGetValue(pieces[2], out CachedFile _))
                return Errno.ENOENT;

            if(pieces.Length > 3)
                return Errno.ENOSYS;

            return mode.HasFlag(AccessModes.W_OK) ? Errno.EROFS : 0;
        }

        protected override Errno OnCreateHandle(string file, OpenedPathInfo info, FilePermissions mode) => Errno.EROFS;

        protected override Errno OnTruncateHandle(string file, OpenedPathInfo info, long length) => Errno.EROFS;

        protected override Errno OnGetHandleStatus(string file, OpenedPathInfo info, out Stat buf)
        {
            buf = new Stat();

            if(!_fileStatHandleCache.TryGetValue(info.Handle.ToInt64(), out Stat fileStat))
                return Errno.EBADF;

            buf = fileStat;

            return 0;
        }

        protected override Errno OnLockHandle(string file, OpenedPathInfo info, FcntlCommand cmd, ref Flock @lock) =>
            Errno.EOPNOTSUPP;

        protected override Errno OnMapPathLogicalToPhysicalIndex(string path, ulong logical, out ulong physical)
        {
            physical = ulong.MaxValue;

            return Errno.EOPNOTSUPP;
        }

        sealed class CachedMachine
        {
            public ulong Id   { get; set; }
            public Stat  Stat { get; set; }
        }

        sealed class CachedFile
        {
            public ulong    Id        { get; set; }
            public ulong    Size      { get; set; }
            public string   Crc32     { get; set; }
            public string   Md5       { get; set; }
            public string   Sha1      { get; set; }
            public string   Sha256    { get; set; }
            public string   Sha384    { get; set; }
            public string   Sha512    { get; set; }
            public DateTime CreatedOn { get; set; }
            public DateTime UpdatedOn { get; set; }
        }
    }
}