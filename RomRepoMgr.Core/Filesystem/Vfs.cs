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
        readonly ConcurrentDictionary<long, RomSet> _romSetsCache;
        Fuse                                        _fuse;

        ConcurrentDictionary<string, long> _rootDirectoryCache;
        Winfsp                             _winfsp;

        public Vfs()
        {
            _rootDirectoryCache = new ConcurrentDictionary<string, long>();
            _romSetsCache       = new ConcurrentDictionary<long, RomSet>();
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
    }
}