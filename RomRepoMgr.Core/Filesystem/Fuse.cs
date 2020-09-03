using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using Mono.Fuse.NETStandard;
using Mono.Unix.Native;
using RomRepoMgr.Database;
using RomRepoMgr.Database.Models;

namespace RomRepoMgr.Core.Filesystem
{
    // TODO: Last handle goes negative
    public sealed class Fuse : FileSystem
    {
        readonly ConcurrentDictionary<long, List<DirectoryEntry>> _directoryCache;
        readonly ConcurrentDictionary<long, Stat>                 _fileStatHandleCache;
        readonly Vfs                                              _vfs;
        long                                                      _lastHandle;
        string                                                    _umountToken;

        public Fuse(Vfs vfs)
        {
            _directoryCache      = new ConcurrentDictionary<long, List<DirectoryEntry>>();
            _lastHandle          = 0;
            _fileStatHandleCache = new ConcurrentDictionary<long, Stat>();
            Name                 = "romrepombgrfs";
            _vfs                 = vfs;
        }

        public static bool IsAvailable
        {
            get
            {
                try
                {
                    IntPtr fuse = dlopen("libfuse.so.2", 2);

                    if(fuse == IntPtr.Zero)
                        return false;

                    dlclose(fuse);

                    IntPtr helper = dlopen("libMonoFuseHelper.so", 2);

                    if(helper == IntPtr.Zero)
                    {
                        helper = dlopen("./libMonoFuseHelper.so", 2);

                        if(helper == IntPtr.Zero)
                            return false;
                    }

                    dlclose(helper);

                    return true;
                }
                catch(Exception e)
                {
                    return false;
                }
            }
        }

        [DllImport("libdl")]
        static extern IntPtr dlopen(string filename, int flags);

        [DllImport("libdl")]
        static extern int dlclose(IntPtr handle);

        protected override Errno OnGetPathStatus(string path, out Stat stat)
        {
            stat = new Stat();

            string[] pieces = _vfs.SplitPath(path);

            if(pieces.Length == 0)
            {
                stat.st_mode  = FilePermissions.S_IFDIR | NativeConvert.FromOctalPermissionString("0555");
                stat.st_nlink = 2;

                return 0;
            }

            long romSetId = _vfs.GetRomSetId(pieces[0]);

            if(romSetId <= 0)
            {
                if(pieces[0]    != ".fuse_umount" ||
                   _umountToken == null)
                    return Errno.ENOENT;

                stat = new Stat
                {
                    st_mode    = FilePermissions.S_IFREG | NativeConvert.FromOctalPermissionString("0444"),
                    st_nlink   = 1,
                    st_ctime   = NativeConvert.ToTimeT(DateTime.UtcNow),
                    st_mtime   = NativeConvert.ToTimeT(DateTime.UtcNow),
                    st_blksize = 0,
                    st_blocks  = 0,
                    st_ino     = 0,
                    st_size    = 0
                };

                return 0;
            }

            RomSet romSet = _vfs.GetRomSet(romSetId);

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

            CachedMachine machine = _vfs.GetMachine(romSetId, pieces[1]);

            if(machine == null)
                return Errno.ENOENT;

            if(pieces.Length == 2)
            {
                stat = new Stat
                {
                    st_mode  = FilePermissions.S_IFDIR | NativeConvert.FromOctalPermissionString("0555"),
                    st_nlink = 2,
                    st_ctime = NativeConvert.ToTimeT(machine.CreationDate.ToUniversalTime()),
                    st_mtime = NativeConvert.ToTimeT(machine.ModificationDate.ToUniversalTime())
                };

                return 0;
            }

            CachedFile file = _vfs.GetFile(machine.Id, pieces[2]);

            if(file == null)
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
            string[] pieces = _vfs.SplitPath(path);

            if(pieces.Length == 0)
                return Errno.EISDIR;

            long romSetId = _vfs.GetRomSetId(pieces[0]);

            if(romSetId <= 0)
                return Errno.ENOENT;

            RomSet romSet = _vfs.GetRomSet(romSetId);

            if(romSet == null)
                return Errno.ENOENT;

            if(pieces.Length == 1)
                return Errno.EISDIR;

            CachedMachine machine = _vfs.GetMachine(romSetId, pieces[1]);

            if(machine == null)
                return Errno.ENOENT;

            if(pieces.Length == 2)
                return Errno.EISDIR;

            CachedFile file = _vfs.GetFile(machine.Id, pieces[2]);

            if(file == null)
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

            long handle = _vfs.Open(file.Sha384, (long)file.Size);

            if(handle <= 0)
                return Errno.ENOENT;

            info.Handle = new IntPtr(handle);

            _fileStatHandleCache[handle] = new Stat
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
            bytesWritten = _vfs.Read(info.Handle.ToInt64(), buf, offset);

            if(bytesWritten >= 0)
                return 0;

            bytesWritten = 0;

            return Errno.EBADF;
        }

        protected override Errno OnWriteHandle(string file, OpenedPathInfo info, byte[] buf, long offset,
                                               out int bytesRead)
        {
            bytesRead = 0;

            return Errno.EROFS;
        }

        protected override Errno OnGetFileSystemStatus(string path, out Statvfs buf)
        {
            _vfs.GetInfo(out ulong files, out ulong totalSize);

            buf = new Statvfs
            {
                f_bsize   = 512,
                f_frsize  = 512,
                f_blocks  = totalSize / 512,
                f_bavail  = 0,
                f_files   = files,
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
            if(!_vfs.Close(info.Handle.ToInt64()))
                return Errno.EBADF;

            _fileStatHandleCache.TryRemove(info.Handle.ToInt64(), out _);

            return 0;
        }

        protected override Errno OnSynchronizeHandle(string file, OpenedPathInfo info, bool onlyUserData) =>
            Errno.EOPNOTSUPP;

        protected override Errno OnSetPathExtendedAttribute(string path, string name, byte[] value, XattrFlags flags)
        {
            if(_umountToken == null)
                return Errno.EROFS;

            if(path != "/.fuse_umount")
                return Errno.EROFS;

            if(name != _umountToken)
                return Errno.EROFS;

            if(value?.Length != 0)
                return Errno.EROFS;

            _umountToken = null;
            Stop();

            return 0;
        }

        protected override Errno OnGetPathExtendedAttribute(string path, string name, byte[] value,
                                                            out int bytesWritten)
        {
            bytesWritten = 0;

            string[] pieces = _vfs.SplitPath(path);

            if(pieces.Length == 0)
                return Errno.ENODATA;

            long romSetId = _vfs.GetRomSetId(pieces[0]);

            if(romSetId <= 0)
                return Errno.ENOENT;

            RomSet romSet = _vfs.GetRomSet(romSetId);

            if(romSet == null)
                return Errno.ENOENT;

            if(pieces.Length == 1)
                return Errno.ENODATA;

            CachedMachine machine = _vfs.GetMachine(romSetId, pieces[1]);

            if(machine == null)
                return Errno.ENOENT;

            if(pieces.Length == 2)
                return Errno.ENODATA;

            CachedFile file = _vfs.GetFile(machine.Id, pieces[2]);

            if(file == null)
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

            string[] pieces = _vfs.SplitPath(path);

            if(pieces.Length == 0)
                return 0;

            long romSetId = _vfs.GetRomSetId(pieces[0]);

            if(romSetId <= 0)
                return Errno.ENOENT;

            RomSet romSet = _vfs.GetRomSet(romSetId);

            if(romSet == null)
                return Errno.ENOENT;

            if(pieces.Length == 1)
                return 0;

            CachedMachine machine = _vfs.GetMachine(romSetId, pieces[1]);

            if(machine == null)
                return Errno.ENOENT;

            if(pieces.Length == 2)
                return 0;

            CachedFile file = _vfs.GetFile(machine.Id, pieces[2]);

            if(file == null)
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

        protected override Errno OnOpenDirectory(string directory, OpenedPathInfo info)
        {
            try
            {
                if(directory == "/")
                {
                    List<DirectoryEntry> entries = new List<DirectoryEntry>
                    {
                        new DirectoryEntry("."),
                        new DirectoryEntry("..")
                    };

                    entries.AddRange(_vfs.GetRootEntries().Select(e => new DirectoryEntry(e)));

                    _lastHandle++;
                    info.Handle = new IntPtr(_lastHandle);

                    _directoryCache[_lastHandle] = entries;

                    return 0;
                }

                string[] pieces = directory.Split("/", StringSplitOptions.RemoveEmptyEntries);

                if(pieces.Length == 0)
                    return Errno.ENOENT;

                long romSetId = _vfs.GetRomSetId(pieces[0]);

                if(romSetId <= 0)
                    return Errno.ENOENT;

                RomSet romSet = _vfs.GetRomSet(romSetId);

                if(romSet == null)
                    return Errno.ENOENT;

                ConcurrentDictionary<string, CachedMachine> machines = _vfs.GetMachinesFromRomSet(romSetId);

                if(pieces.Length == 1)
                {
                    List<DirectoryEntry> entries = new List<DirectoryEntry>
                    {
                        new DirectoryEntry("."),
                        new DirectoryEntry("..")
                    };

                    entries.AddRange(machines.Select(mach => new DirectoryEntry(mach.Key)));

                    _lastHandle++;
                    info.Handle = new IntPtr(_lastHandle);

                    _directoryCache[_lastHandle] = entries;

                    return 0;
                }

                CachedMachine machine = _vfs.GetMachine(romSetId, pieces[1]);

                if(machine == null)
                    return Errno.ENOENT;

                ConcurrentDictionary<string, CachedFile> cachedMachineFiles = _vfs.GetFilesFromMachine(machine.Id);

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
            string[] pieces = _vfs.SplitPath(path);

            if(pieces.Length == 0)
                return mode.HasFlag(AccessModes.W_OK) ? Errno.EROFS : 0;

            long romSetId = _vfs.GetRomSetId(pieces[0]);

            if(romSetId <= 0)
                return Errno.ENOENT;

            RomSet romSet = _vfs.GetRomSet(romSetId);

            if(romSet == null)
                return Errno.ENOENT;

            if(pieces.Length == 1)
                return mode.HasFlag(AccessModes.W_OK) ? Errno.EROFS : 0;

            CachedMachine machine = _vfs.GetMachine(romSetId, pieces[1]);

            if(machine == null)
                return Errno.ENOENT;

            if(pieces.Length == 2)
                return mode.HasFlag(AccessModes.W_OK) ? Errno.EROFS : 0;

            CachedFile file = _vfs.GetFile(machine.Id, pieces[2]);

            if(file == null)
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

        [DllImport("libc", SetLastError = true)]
        static extern int setxattr(string path, string name, IntPtr value, long size, int flags);

        public void Umount()
        {
            var    rnd   = new Random();
            byte[] token = new byte[64];
            rnd.NextBytes(token);
            _umountToken = Base32.ToBase32String(token);
            setxattr(Path.Combine(MountPoint, ".fuse_umount"), _umountToken, IntPtr.Zero, 0, 0);
        }
    }
}