using System;
using System.Threading.Tasks;

namespace RomRepoMgr.Core.Filesystem
{
    public class Vfs : IDisposable
    {
        Fuse _fuse;

        public static bool IsAvailable => Winfsp.IsAvailable || Fuse.IsAvailable;

        public void Dispose() => _fuse?.Dispose();

        public event EventHandler<System.EventArgs> Umounted;

        public void MountTo(string result)
        {
            if(Fuse.IsAvailable)
            {
                _fuse = new Fuse
                {
                    MountPoint = result
                };

                Task.Run(() =>
                {
                    _fuse.Start();

                    Umounted?.Invoke(this, System.EventArgs.Empty);
                });
            }
        }

        public void Umount()
        {
            _fuse?.Umount();
            _fuse = null;
        }
    }
}