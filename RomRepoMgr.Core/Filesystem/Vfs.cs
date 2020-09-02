using System;
using System.Threading.Tasks;

namespace RomRepoMgr.Core.Filesystem
{
    public class Vfs : IDisposable
    {
        Fuse   _fuse;
        Winfsp _winfsp;

        public static bool IsAvailable => Winfsp.IsAvailable || Fuse.IsAvailable;

        public void Dispose() => _fuse?.Dispose();

        public event EventHandler<System.EventArgs> Umounted;

        public void MountTo(string mountPoint)
        {
            if(Fuse.IsAvailable)
            {
                _fuse = new Fuse
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
                _winfsp = new Winfsp();
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
    }
}