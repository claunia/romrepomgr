using System;
using System.Collections.Concurrent;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using RomRepoMgr.Database;
using RomRepoMgr.Database.Models;

namespace RomRepoMgr.Core.Filesystem
{
    public class Vfs : IDisposable
    {
        readonly ConcurrentDictionary<ulong, ConcurrentDictionary<string, CachedFile>>   _machineFilesCache;
        readonly ConcurrentDictionary<long, ConcurrentDictionary<string, CachedMachine>> _machinesStatCache;
        readonly ConcurrentDictionary<long, RomSet>                                      _romSetsCache;
        Fuse                                                                             _fuse;
        ConcurrentDictionary<string, long>                                               _rootDirectoryCache;
        Winfsp                                                                           _winfsp;

        public Vfs()
        {
            _rootDirectoryCache = new ConcurrentDictionary<string, long>();
            _romSetsCache       = new ConcurrentDictionary<long, RomSet>();
            _machinesStatCache  = new ConcurrentDictionary<long, ConcurrentDictionary<string, CachedMachine>>();
            _machineFilesCache  = new ConcurrentDictionary<ulong, ConcurrentDictionary<string, CachedFile>>();
        }

        public static bool IsAvailable => Winfsp.IsAvailable || Fuse.IsAvailable;

        public void Dispose() => _fuse?.Dispose();

        public event EventHandler<System.EventArgs> Umounted;

        public void MountTo(string mountPoint)
        {
            if(Fuse.IsAvailable)
            {
                _fuse = new Fuse(this)
                {
                    MountPoint = mountPoint
                };

                Task.Run(() =>
                {
                    _fuse.Start();

                    Umounted?.Invoke(this, System.EventArgs.Empty);
                });
            }
            else if(Winfsp.IsAvailable)
            {
                _winfsp = new Winfsp(this);
                bool ret = _winfsp.Mount(mountPoint);

                if(ret)
                    return;

                _winfsp = null;
                Umounted?.Invoke(this, System.EventArgs.Empty);
            }
            else
                Umounted?.Invoke(this, System.EventArgs.Empty);
        }

        public void Umount()
        {
            _fuse?.Umount();
            _fuse = null;
            _winfsp?.Umount();
            _winfsp = null;

            Umounted?.Invoke(this, System.EventArgs.Empty);
        }

        internal void GetInfo(out ulong files, out ulong totalSize)
        {
            using var ctx = Context.Create(Settings.Settings.Current.DatabasePath);

            totalSize = (ulong)ctx.Files.Where(f => f.IsInRepo).Sum(f => (double)f.Size);
            files     = (ulong)ctx.Files.Count(f => f.IsInRepo);
        }

        internal string[] SplitPath(string path) =>
            path.Split(RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "\\" : "/",
                       StringSplitOptions.RemoveEmptyEntries);

        void FillRootDirectoryCache()
        {
            using var ctx = Context.Create(Settings.Settings.Current.DatabasePath);

            ConcurrentDictionary<string, long> rootCache = new ConcurrentDictionary<string, long>();

            foreach(RomSet set in ctx.RomSets)
            {
                string name;

                if(RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                {
                    name = set.Name.Replace('/', '∕').Replace('<', '\uFF1C').Replace('>', '\uFF1E').
                               Replace(':', '\uFF1A').Replace('"', '\u2033').Replace('\\', '＼').Replace('|', '｜').
                               Replace('?', '？').Replace('*', '＊');

                    if(rootCache.ContainsKey(name))
                        name = Path.GetFileNameWithoutExtension(set.Filename)?.Replace('/', '∕').Replace('<', '\uFF1C').
                                    Replace('>', '\uFF1E').Replace(':', '\uFF1A').Replace('"', '\u2033').
                                    Replace('\\', '＼').Replace('|', '｜').Replace('?', '？').Replace('*', '＊');
                }
                else
                {
                    name = set.Name.Replace('/', '∕');

                    if(rootCache.ContainsKey(name))
                        name = Path.GetFileNameWithoutExtension(set.Filename)?.Replace('/', '∕');
                }

                if(name == null ||
                   rootCache.ContainsKey(name))
                    name = Path.GetFileNameWithoutExtension(set.Sha384);

                if(name == null)
                    continue;

                rootCache[name]       = set.Id;
                _romSetsCache[set.Id] = set;
            }

            _rootDirectoryCache = rootCache;
        }

        internal long GetRomSetId(string name)
        {
            if(_rootDirectoryCache.Count == 0)
                FillRootDirectoryCache();

            if(!_rootDirectoryCache.TryGetValue(name, out long romSetId))
                return -1;

            return romSetId;
        }

        internal RomSet GetRomSet(long id)
        {
            if(_romSetsCache.TryGetValue(id, out RomSet romSet))
                return romSet;

            using var ctx = Context.Create(Settings.Settings.Current.DatabasePath);

            romSet = ctx.RomSets.Find(id);

            if(romSet == null)
                return null;

            _romSetsCache[id] = romSet;

            return romSet;
        }

        internal ConcurrentDictionary<string, CachedMachine> GetMachinesFromRomSet(long id)
        {
            _machinesStatCache.TryGetValue(id, out ConcurrentDictionary<string, CachedMachine> cachedMachines);

            if(cachedMachines != null)
                return cachedMachines;

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

            if(cachedMachines == null ||
               !cachedMachines.TryGetValue(name, out CachedMachine machine))
                return null;

            return machine;
        }

        internal ConcurrentDictionary<string, CachedFile> GetFilesFromMachine(ulong id)
        {
            _machineFilesCache.TryGetValue(id, out ConcurrentDictionary<string, CachedFile> cachedMachineFiles);

            if(cachedMachineFiles != null)
                return cachedMachineFiles;

            using var ctx = Context.Create(Settings.Settings.Current.DatabasePath);

            cachedMachineFiles = new ConcurrentDictionary<string, CachedFile>();

            foreach(FileByMachine machineFile in ctx.FilesByMachines.Where(fbm => fbm.Machine.Id == id &&
                                                                               fbm.File.IsInRepo))
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

            _machineFilesCache[id] = cachedMachineFiles;

            return cachedMachineFiles;
        }

        internal CachedFile GetFile(ulong machineId, string name)
        {
            ConcurrentDictionary<string, CachedFile> cachedFiles = GetFilesFromMachine(machineId);

            if(cachedFiles == null ||
               !cachedFiles.TryGetValue(name, out CachedFile file))
                return null;

            return file;
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