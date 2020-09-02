using System;
using Fsp;
using Fsp.Interop;

namespace RomRepoMgr.Core.Filesystem
{
    public class Winfsp : FileSystemBase
    {
        FileSystemHost _host;

        public static bool IsAvailable
        {
            get
            {
                try
                {
                    Version winfspVersion = FileSystemHost.Version();

                    if(winfspVersion == null)
                        return false;

                    return winfspVersion.Major == 1 && winfspVersion.Minor >= 7;
                }
                catch(Exception)
                {
                    return false;
                }
            }
        }

        internal bool Mount(string mountPoint)
        {
            _host = new FileSystemHost(this);
            int ret = _host.Mount(mountPoint);

            if(ret == STATUS_SUCCESS)
                return true;

            _host = null;

            return false;
        }

        internal void Umount() => _host?.Unmount();

        public override int SetVolumeLabel(string VolumeLabel, out VolumeInfo VolumeInfo)
        {
            VolumeInfo = default;

            return STATUS_MEDIA_WRITE_PROTECTED;
        }
    }
}