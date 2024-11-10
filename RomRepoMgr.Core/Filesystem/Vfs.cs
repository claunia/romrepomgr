using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using RomRepoMgr.Database;
using RomRepoMgr.Database.Models;
using SharpCompress.Compressors;
using SharpCompress.Compressors.LZMA;

namespace RomRepoMgr.Core.Filesystem;

// TODO: Last handle goes negative
// TODO: Invalidate caches
// TODO: Mount options
// TODO: Do not show machines or romsets with no ROMs in repo
public class Vfs : IDisposable
{
    readonly ConcurrentDictionary<ulong, ConcurrentDictionary<string, CachedDisk>>   _machineDisksCache;
    readonly ConcurrentDictionary<ulong, ConcurrentDictionary<string, CachedFile>>   _machineFilesCache;
    readonly ConcurrentDictionary<ulong, ConcurrentDictionary<string, CachedMedia>>  _machineMediasCache;
    readonly ConcurrentDictionary<long, ConcurrentDictionary<string, CachedMachine>> _machinesStatCache;
    readonly ConcurrentDictionary<long, RomSet>                                      _romSetsCache;
    readonly ConcurrentDictionary<long, Stream>                                      _streamsCache;
    Fuse                                                                             _fuse;
    long                                                                             _lastHandle;
    ConcurrentDictionary<string, long>                                               _rootDirectoryCache;
    Winfsp                                                                           _winfsp;

    public Vfs()
    {
        _rootDirectoryCache = new ConcurrentDictionary<string, long>();
        _romSetsCache       = new ConcurrentDictionary<long, RomSet>();
        _machinesStatCache  = new ConcurrentDictionary<long, ConcurrentDictionary<string, CachedMachine>>();
        _machineFilesCache  = new ConcurrentDictionary<ulong, ConcurrentDictionary<string, CachedFile>>();
        _machineDisksCache  = new ConcurrentDictionary<ulong, ConcurrentDictionary<string, CachedDisk>>();
        _machineMediasCache = new ConcurrentDictionary<ulong, ConcurrentDictionary<string, CachedMedia>>();
        _streamsCache       = new ConcurrentDictionary<long, Stream>();
        _lastHandle         = 0;
    }

    public static bool IsAvailable => OperatingSystem.IsMacOS() || OperatingSystem.IsLinux()
                                          ? Fuse.IsAvailable
                                          : OperatingSystem.IsWindows() && Winfsp.IsAvailable;

    public void Dispose() => Umount();

    public event EventHandler<System.EventArgs> Umounted;

    public void MountTo(string mountPoint)
    {
        if((OperatingSystem.IsMacOS() || OperatingSystem.IsLinux()) && Fuse.IsAvailable)
        {
            _fuse = new Fuse(this)
            {
                MountPoint = mountPoint
            };

            Task.Run(() =>
            {
                _fuse.Start();

                CleanUp();
            });
        }
        else if(OperatingSystem.IsWindows() && Winfsp.IsAvailable)
        {
            _winfsp = new Winfsp(this);
            bool ret = _winfsp.Mount(mountPoint);

            if(ret) return;

            _winfsp = null;
            CleanUp();
        }
        else
            CleanUp();
    }

    public void Umount()
    {
        if(OperatingSystem.IsMacOS() || OperatingSystem.IsLinux())
        {
            _fuse?.Umount();
            _fuse = null;
        }

        if(OperatingSystem.IsWindows())
        {
            _winfsp?.Umount();
            _winfsp = null;
        }

        CleanUp();
    }

    public void CleanUp()
    {
        foreach(KeyValuePair<long, Stream> handle in _streamsCache) handle.Value.Close();

        _streamsCache.Clear();
        _lastHandle = 0;

        Umounted?.Invoke(this, System.EventArgs.Empty);
    }

    internal void GetInfo(out ulong files, out ulong totalSize)
    {
        using var ctx = Context.Create(Settings.Settings.Current.DatabasePath);

        totalSize = (ulong)(ctx.Files.Where(f => f.IsInRepo).Sum(f => (double)f.Size) +
                            ctx.Disks.Where(f => f.IsInRepo).Sum(f => (double)f.Size) +
                            ctx.Medias.Where(f => f.IsInRepo).Sum(f => (double)f.Size));

        files = (ulong)(ctx.Files.Count(f => f.IsInRepo) +
                        ctx.Disks.Count(f => f.IsInRepo) +
                        ctx.Medias.Count(f => f.IsInRepo));
    }

    internal string[] SplitPath(string path) =>
        path.Split(OperatingSystem.IsWindows() ? "\\" : "/", StringSplitOptions.RemoveEmptyEntries);

    void FillRootDirectoryCache()
    {
        using var ctx = Context.Create(Settings.Settings.Current.DatabasePath);

        var rootCache = new ConcurrentDictionary<string, long>();

        foreach(RomSet set in ctx.RomSets)
        {
            string name;

            if(OperatingSystem.IsWindows())
            {
                name = set.Name.Replace('/', '∕')
                          .Replace('<',  '\uFF1C')
                          .Replace('>',  '\uFF1E')
                          .Replace(':',  '\uFF1A')
                          .Replace('"',  '\u2033')
                          .Replace('\\', '＼')
                          .Replace('|',  '｜')
                          .Replace('?',  '？')
                          .Replace('*',  '＊');

                if(rootCache.ContainsKey(name))
                {
                    name = Path.GetFileNameWithoutExtension(set.Filename)
                              ?.Replace('/', '∕')
                               .Replace('<',  '\uFF1C')
                               .Replace('>',  '\uFF1E')
                               .Replace(':',  '\uFF1A')
                               .Replace('"',  '\u2033')
                               .Replace('\\', '＼')
                               .Replace('|',  '｜')
                               .Replace('?',  '？')
                               .Replace('*',  '＊');
                }
            }
            else
            {
                name = set.Name.Replace('/', '∕');

                if(rootCache.ContainsKey(name))
                    name = Path.GetFileNameWithoutExtension(set.Filename)?.Replace('/', '∕');
            }

            if(name == null || rootCache.ContainsKey(name)) name = Path.GetFileNameWithoutExtension(set.Sha384);

            if(name == null) continue;

            rootCache[name]       = set.Id;
            _romSetsCache[set.Id] = set;
        }

        _rootDirectoryCache = rootCache;
    }

    internal long GetRomSetId(string name)
    {
        if(_rootDirectoryCache.Count == 0) FillRootDirectoryCache();

        if(!_rootDirectoryCache.TryGetValue(name, out long romSetId)) return -1;

        return romSetId;
    }

    internal RomSet GetRomSet(long id)
    {
        if(_romSetsCache.TryGetValue(id, out RomSet romSet)) return romSet;

        using var ctx = Context.Create(Settings.Settings.Current.DatabasePath);

        romSet = ctx.RomSets.Find(id);

        if(romSet == null) return null;

        _romSetsCache[id] = romSet;

        return romSet;
    }

    internal ConcurrentDictionary<string, CachedMachine> GetMachinesFromRomSet(long id)
    {
        _machinesStatCache.TryGetValue(id, out ConcurrentDictionary<string, CachedMachine> cachedMachines);

        if(cachedMachines != null) return cachedMachines;

        cachedMachines = new ConcurrentDictionary<string, CachedMachine>();

        using var ctx = Context.Create(Settings.Settings.Current.DatabasePath);

        foreach(Machine mach in ctx.Machines.Where(m => m.RomSet.Id == id))
        {
            cachedMachines[mach.Name] = new CachedMachine
            {
                Id               = mach.Id,
                CreationDate     = mach.CreatedOn,
                ModificationDate = mach.UpdatedOn
            };
        }

        _machinesStatCache[id] = cachedMachines;

        return cachedMachines;
    }

    internal CachedMachine GetMachine(long romSetId, string name)
    {
        ConcurrentDictionary<string, CachedMachine> cachedMachines = GetMachinesFromRomSet(romSetId);

        if(cachedMachines == null || !cachedMachines.TryGetValue(name, out CachedMachine machine)) return null;

        return machine;
    }

    internal ConcurrentDictionary<string, CachedFile> GetFilesFromMachine(ulong id)
    {
        _machineFilesCache.TryGetValue(id, out ConcurrentDictionary<string, CachedFile> cachedMachineFiles);

        if(cachedMachineFiles != null) return cachedMachineFiles;

        using var ctx = Context.Create(Settings.Settings.Current.DatabasePath);

        cachedMachineFiles = new ConcurrentDictionary<string, CachedFile>();

        foreach(FileByMachine machineFile in
                ctx.FilesByMachines.Where(fbm => fbm.Machine.Id == id && fbm.File.IsInRepo))
        {
            var cachedFile = new CachedFile
            {
                Id                   = machineFile.File.Id,
                Crc32                = machineFile.File.Crc32,
                Md5                  = machineFile.File.Md5,
                Sha1                 = machineFile.File.Sha1,
                Sha256               = machineFile.File.Sha256,
                Sha384               = machineFile.File.Sha384,
                Sha512               = machineFile.File.Sha512,
                Size                 = machineFile.File.Size,
                CreatedOn            = machineFile.File.CreatedOn,
                UpdatedOn            = machineFile.File.UpdatedOn,
                FileLastModification = machineFile.FileLastModification
            };

            cachedMachineFiles[machineFile.Name] = cachedFile;
        }

        _machineFilesCache[id] = cachedMachineFiles;

        return cachedMachineFiles;
    }

    internal ConcurrentDictionary<string, CachedDisk> GetDisksFromMachine(ulong id)
    {
        _machineDisksCache.TryGetValue(id, out ConcurrentDictionary<string, CachedDisk> cachedMachineDisks);

        if(cachedMachineDisks != null) return cachedMachineDisks;

        using var ctx = Context.Create(Settings.Settings.Current.DatabasePath);

        cachedMachineDisks = new ConcurrentDictionary<string, CachedDisk>();

        foreach(DiskByMachine machineDisk in ctx.DisksByMachines.Where(dbm => dbm.Machine.Id == id &&
                                                                              dbm.Disk.IsInRepo    &&
                                                                              dbm.Disk.Size != null))
        {
            var cachedDisk = new CachedDisk
            {
                Id        = machineDisk.Disk.Id,
                Md5       = machineDisk.Disk.Md5,
                Sha1      = machineDisk.Disk.Sha1,
                Size      = machineDisk.Disk.Size ?? 0,
                CreatedOn = machineDisk.Disk.CreatedOn,
                UpdatedOn = machineDisk.Disk.UpdatedOn
            };

            cachedMachineDisks[machineDisk.Name] = cachedDisk;
        }

        _machineDisksCache[id] = cachedMachineDisks;

        return cachedMachineDisks;
    }

    internal ConcurrentDictionary<string, CachedMedia> GetMediasFromMachine(ulong id)
    {
        _machineMediasCache.TryGetValue(id, out ConcurrentDictionary<string, CachedMedia> cachedMachineMedias);

        if(cachedMachineMedias != null) return cachedMachineMedias;

        using var ctx = Context.Create(Settings.Settings.Current.DatabasePath);

        cachedMachineMedias = new ConcurrentDictionary<string, CachedMedia>();

        foreach(MediaByMachine machineMedia in ctx.MediasByMachines.Where(mbm => mbm.Machine.Id == id &&
                                                                              mbm.Media.IsInRepo      &&
                                                                              mbm.Media.Size != null))
        {
            var cachedDisk = new CachedMedia
            {
                Id        = machineMedia.Media.Id,
                Md5       = machineMedia.Media.Md5,
                Sha1      = machineMedia.Media.Sha1,
                Sha256    = machineMedia.Media.Sha256,
                SpamSum   = machineMedia.Media.SpamSum,
                Size      = machineMedia.Media.Size ?? 0,
                CreatedOn = machineMedia.Media.CreatedOn,
                UpdatedOn = machineMedia.Media.UpdatedOn
            };

            cachedMachineMedias[machineMedia.Name] = cachedDisk;
        }

        _machineMediasCache[id] = cachedMachineMedias;

        return cachedMachineMedias;
    }

    internal CachedFile GetFile(ulong machineId, string name)
    {
        ConcurrentDictionary<string, CachedFile> cachedFiles = GetFilesFromMachine(machineId);

        if(cachedFiles == null || !cachedFiles.TryGetValue(name, out CachedFile file)) return null;

        return file;
    }

    internal CachedDisk GetDisk(ulong machineId, string name)
    {
        if(name.EndsWith(".chd", StringComparison.OrdinalIgnoreCase)) name = name.Substring(0, name.Length - 4);

        ConcurrentDictionary<string, CachedDisk> cachedDisks = GetDisksFromMachine(machineId);

        if(cachedDisks == null || !cachedDisks.TryGetValue(name, out CachedDisk disk)) return null;

        return disk;
    }

    internal CachedMedia GetMedia(ulong machineId, string name)
    {
        if(name.EndsWith(".aif", StringComparison.OrdinalIgnoreCase)) name = name.Substring(0, name.Length - 4);

        ConcurrentDictionary<string, CachedMedia> cachedMedias = GetMediasFromMachine(machineId);

        if(cachedMedias == null || !cachedMedias.TryGetValue(name, out CachedMedia media)) return null;

        return media;
    }

    internal long Open(string sha384, long fileSize)
    {
        var sha384Bytes = new byte[48];

        for(var i = 0; i < 48; i++)
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
                                       sha384B32[4].ToString(),
                                       sha384B32 + ".lz");

        if(!File.Exists(repoPath)) return -1;

        _lastHandle++;
        long handle = _lastHandle;

        _streamsCache[handle] =
            Stream.Synchronized(new ForcedSeekStream<LZipStream>(fileSize,
                                                                 new FileStream(repoPath,
                                                                                    FileMode.Open,
                                                                                    FileAccess.Read),
                                                                 CompressionMode.Decompress));

        return handle;
    }

    internal int Read(long handle, byte[] buf, long offset)
    {
        if(!_streamsCache.TryGetValue(handle, out Stream stream)) return -1;

        using var rwLock = new ReaderWriterLockSlim();

        try
        {
            rwLock.EnterReadLock();

            stream.Position = offset;

            return stream.Read(buf, 0, buf.Length);
        }
        finally
        {
            rwLock.ExitReadLock();
        }
    }

    internal bool Close(long handle)
    {
        if(!_streamsCache.TryGetValue(handle, out Stream stream)) return false;

        stream.Close();
        _streamsCache.TryRemove(handle, out _);

        return true;
    }

    internal IEnumerable<string> GetRootEntries()
    {
        if(_rootDirectoryCache.Count == 0) FillRootDirectoryCache();

        return _rootDirectoryCache.Keys.ToArray();
    }

    public long OpenDisk(string sha1, string md5)
    {
        if(sha1 == null && md5 == null) return -1;

        string repoPath = null;
        string md5Path  = null;
        string sha1Path = null;

        if(sha1 != null)
        {
            var sha1Bytes = new byte[20];

            for(var i = 0; i < 20; i++)
            {
                if(sha1[i * 2] >= 0x30 && sha1[i * 2] <= 0x39)
                    sha1Bytes[i] = (byte)((sha1[i * 2] - 0x30) * 0x10);
                else if(sha1[i * 2] >= 0x41 && sha1[i * 2] <= 0x46)
                    sha1Bytes[i]                                                 = (byte)((sha1[i * 2] - 0x37) * 0x10);
                else if(sha1[i * 2] >= 0x61 && sha1[i * 2] <= 0x66) sha1Bytes[i] = (byte)((sha1[i * 2] - 0x57) * 0x10);

                if(sha1[i * 2 + 1] >= 0x30 && sha1[i * 2 + 1] <= 0x39)
                    sha1Bytes[i] += (byte)(sha1[i * 2 + 1] - 0x30);
                else if(sha1[i * 2 + 1] >= 0x41 && sha1[i * 2 + 1] <= 0x46)
                    sha1Bytes[i] += (byte)(sha1[i * 2 + 1] - 0x37);
                else if(sha1[i * 2 + 1] >= 0x61 && sha1[i * 2 + 1] <= 0x66)
                    sha1Bytes[i] += (byte)(sha1[i * 2 + 1] - 0x57);
            }

            string sha1B32 = Base32.ToBase32String(sha1Bytes);

            sha1Path = Path.Combine(Settings.Settings.Current.RepositoryPath,
                                    "chd",
                                    "sha1",
                                    sha1B32[0].ToString(),
                                    sha1B32[1].ToString(),
                                    sha1B32[2].ToString(),
                                    sha1B32[3].ToString(),
                                    sha1B32[4].ToString(),
                                    sha1B32 + ".chd");
        }

        if(md5 != null)
        {
            var md5Bytes = new byte[16];

            for(var i = 0; i < 16; i++)
            {
                if(md5[i * 2] >= 0x30 && md5[i * 2] <= 0x39)
                    md5Bytes[i] = (byte)((md5[i * 2] - 0x30) * 0x10);
                else if(md5[i * 2] >= 0x41 && md5[i * 2] <= 0x46)
                    md5Bytes[i]                                               = (byte)((md5[i * 2] - 0x37) * 0x10);
                else if(md5[i * 2] >= 0x61 && md5[i * 2] <= 0x66) md5Bytes[i] = (byte)((md5[i * 2] - 0x57) * 0x10);

                if(md5[i * 2 + 1] >= 0x30 && md5[i * 2 + 1] <= 0x39)
                    md5Bytes[i] += (byte)(md5[i * 2 + 1] - 0x30);
                else if(md5[i * 2 + 1] >= 0x41 && md5[i * 2 + 1] <= 0x46)
                    md5Bytes[i]                                                       += (byte)(md5[i * 2 + 1] - 0x37);
                else if(md5[i * 2 + 1] >= 0x61 && md5[i * 2 + 1] <= 0x66) md5Bytes[i] += (byte)(md5[i * 2 + 1] - 0x57);
            }

            string md5B32 = Base32.ToBase32String(md5Bytes);

            md5Path = Path.Combine(Settings.Settings.Current.RepositoryPath,
                                   "chd",
                                   "md5",
                                   md5B32[0].ToString(),
                                   md5B32[1].ToString(),
                                   md5B32[2].ToString(),
                                   md5B32[3].ToString(),
                                   md5B32[4].ToString(),
                                   md5B32 + ".chd");
        }

        if(File.Exists(sha1Path))
            repoPath                           = sha1Path;
        else if(File.Exists(md5Path)) repoPath = md5Path;

        if(repoPath == null) return -1;

        _lastHandle++;
        long handle = _lastHandle;

        _streamsCache[handle] = Stream.Synchronized(new FileStream(repoPath, FileMode.Open, FileAccess.Read));

        return handle;
    }

    public long OpenMedia(string sha256, string sha1, string md5)
    {
        if(sha256 == null && sha1 == null && md5 == null) return -1;

        string repoPath   = null;
        string md5Path    = null;
        string sha1Path   = null;
        string sha256Path = null;

        if(sha256 != null)
        {
            var sha256Bytes = new byte[32];

            for(var i = 0; i < 32; i++)
            {
                if(sha256[i * 2] >= 0x30 && sha256[i * 2] <= 0x39)
                    sha256Bytes[i] = (byte)((sha256[i * 2] - 0x30) * 0x10);
                else if(sha256[i * 2] >= 0x41 && sha256[i * 2] <= 0x46)
                    sha256Bytes[i] = (byte)((sha256[i * 2] - 0x37) * 0x10);
                else if(sha256[i * 2] >= 0x61 && sha256[i * 2] <= 0x66)
                    sha256Bytes[i] = (byte)((sha256[i * 2] - 0x57) * 0x10);

                if(sha256[i * 2 + 1] >= 0x30 && sha256[i * 2 + 1] <= 0x39)
                    sha256Bytes[i] += (byte)(sha256[i * 2 + 1] - 0x30);
                else if(sha256[i * 2 + 1] >= 0x41 && sha256[i * 2 + 1] <= 0x46)
                    sha256Bytes[i] += (byte)(sha256[i * 2 + 1] - 0x37);
                else if(sha256[i * 2 + 1] >= 0x61 && sha256[i * 2 + 1] <= 0x66)
                    sha256Bytes[i] += (byte)(sha256[i * 2 + 1] - 0x57);
            }

            string sha256B32 = Base32.ToBase32String(sha256Bytes);

            sha256Path = Path.Combine(Settings.Settings.Current.RepositoryPath,
                                      "aaru",
                                      "sha256",
                                      sha256B32[0].ToString(),
                                      sha256B32[1].ToString(),
                                      sha256B32[2].ToString(),
                                      sha256B32[3].ToString(),
                                      sha256B32[4].ToString(),
                                      sha256B32 + ".aif");
        }

        if(sha1 != null)
        {
            var sha1Bytes = new byte[20];

            for(var i = 0; i < 20; i++)
            {
                if(sha1[i * 2] >= 0x30 && sha1[i * 2] <= 0x39)
                    sha1Bytes[i] = (byte)((sha1[i * 2] - 0x30) * 0x10);
                else if(sha1[i * 2] >= 0x41 && sha1[i * 2] <= 0x46)
                    sha1Bytes[i]                                                 = (byte)((sha1[i * 2] - 0x37) * 0x10);
                else if(sha1[i * 2] >= 0x61 && sha1[i * 2] <= 0x66) sha1Bytes[i] = (byte)((sha1[i * 2] - 0x57) * 0x10);

                if(sha1[i * 2 + 1] >= 0x30 && sha1[i * 2 + 1] <= 0x39)
                    sha1Bytes[i] += (byte)(sha1[i * 2 + 1] - 0x30);
                else if(sha1[i * 2 + 1] >= 0x41 && sha1[i * 2 + 1] <= 0x46)
                    sha1Bytes[i] += (byte)(sha1[i * 2 + 1] - 0x37);
                else if(sha1[i * 2 + 1] >= 0x61 && sha1[i * 2 + 1] <= 0x66)
                    sha1Bytes[i] += (byte)(sha1[i * 2 + 1] - 0x57);
            }

            string sha1B32 = Base32.ToBase32String(sha1Bytes);

            sha1Path = Path.Combine(Settings.Settings.Current.RepositoryPath,
                                    "aaru",
                                    "sha1",
                                    sha1B32[0].ToString(),
                                    sha1B32[1].ToString(),
                                    sha1B32[2].ToString(),
                                    sha1B32[3].ToString(),
                                    sha1B32[4].ToString(),
                                    sha1B32 + ".aif");
        }

        if(md5 != null)
        {
            var md5Bytes = new byte[16];

            for(var i = 0; i < 16; i++)
            {
                if(md5[i * 2] >= 0x30 && md5[i * 2] <= 0x39)
                    md5Bytes[i] = (byte)((md5[i * 2] - 0x30) * 0x10);
                else if(md5[i * 2] >= 0x41 && md5[i * 2] <= 0x46)
                    md5Bytes[i]                                               = (byte)((md5[i * 2] - 0x37) * 0x10);
                else if(md5[i * 2] >= 0x61 && md5[i * 2] <= 0x66) md5Bytes[i] = (byte)((md5[i * 2] - 0x57) * 0x10);

                if(md5[i * 2 + 1] >= 0x30 && md5[i * 2 + 1] <= 0x39)
                    md5Bytes[i] += (byte)(md5[i * 2 + 1] - 0x30);
                else if(md5[i * 2 + 1] >= 0x41 && md5[i * 2 + 1] <= 0x46)
                    md5Bytes[i]                                                       += (byte)(md5[i * 2 + 1] - 0x37);
                else if(md5[i * 2 + 1] >= 0x61 && md5[i * 2 + 1] <= 0x66) md5Bytes[i] += (byte)(md5[i * 2 + 1] - 0x57);
            }

            string md5B32 = Base32.ToBase32String(md5Bytes);

            md5Path = Path.Combine(Settings.Settings.Current.RepositoryPath,
                                   "aaru",
                                   "md5",
                                   md5B32[0].ToString(),
                                   md5B32[1].ToString(),
                                   md5B32[2].ToString(),
                                   md5B32[3].ToString(),
                                   md5B32[4].ToString(),
                                   md5B32 + ".aif");
        }

        if(File.Exists(sha256Path))
            repoPath = sha256Path;
        else if(File.Exists(sha1Path))
            repoPath                           = sha1Path;
        else if(File.Exists(md5Path)) repoPath = md5Path;

        if(repoPath == null) return -1;

        _lastHandle++;
        long handle = _lastHandle;

        _streamsCache[handle] = Stream.Synchronized(new FileStream(repoPath, FileMode.Open, FileAccess.Read));

        return handle;
    }
}

internal sealed class CachedMachine
{
    public ulong    Id               { get; set; }
    public DateTime CreationDate     { get; set; }
    public DateTime ModificationDate { get; set; }
}

internal sealed class CachedFile
{
    public ulong     Id                   { get; set; }
    public ulong     Size                 { get; set; }
    public string    Crc32                { get; set; }
    public string    Md5                  { get; set; }
    public string    Sha1                 { get; set; }
    public string    Sha256               { get; set; }
    public string    Sha384               { get; set; }
    public string    Sha512               { get; set; }
    public DateTime  CreatedOn            { get; set; }
    public DateTime  UpdatedOn            { get; set; }
    public DateTime? FileLastModification { get; set; }
}

internal sealed class CachedDisk
{
    public ulong    Id        { get; set; }
    public ulong    Size      { get; set; }
    public string   Md5       { get; set; }
    public string   Sha1      { get; set; }
    public DateTime CreatedOn { get; set; }
    public DateTime UpdatedOn { get; set; }
}

internal sealed class CachedMedia
{
    public ulong    Id        { get; set; }
    public ulong    Size      { get; set; }
    public string   Md5       { get; set; }
    public string   Sha1      { get; set; }
    public string   Sha256    { get; set; }
    public string   SpamSum   { get; set; }
    public DateTime CreatedOn { get; set; }
    public DateTime UpdatedOn { get; set; }
}