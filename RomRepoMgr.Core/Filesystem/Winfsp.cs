using System;
using Fsp;
using Fsp.Interop;

namespace RomRepoMgr.Core.Filesystem
{
    public class Winfsp : FileSystemBase
    {
        readonly Vfs   _vfs;
        FileSystemHost _host;

        public Winfsp(Vfs vfs) => _vfs = vfs;

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

        public override int SetVolumeLabel(string volumeLabel, out VolumeInfo volumeInfo)
        {
            volumeInfo = default;

            return STATUS_MEDIA_WRITE_PROTECTED;
        }

        public override int Create(string fileName, uint createOptions, uint grantedAccess, uint fileAttributes,
                                   byte[] securityDescriptor, ulong allocationSize, out object fileNode,
                                   out object fileDesc, out FileInfo fileInfo, out string normalizedName)
        {
            fileNode       = default;
            fileDesc       = default;
            fileInfo       = default;
            normalizedName = default;

            return STATUS_MEDIA_WRITE_PROTECTED;
        }

        public override int Overwrite(object fileNode, object fileDesc, uint fileAttributes, bool replaceFileAttributes,
                                      ulong allocationSize, out FileInfo fileInfo)
        {
            fileInfo = default;

            return STATUS_MEDIA_WRITE_PROTECTED;
        }

        public override int Write(object fileNode, object fileDesc, IntPtr buffer, ulong offset, uint length,
                                  bool writeToEndOfFile, bool constrainedIo, out uint bytesTransferred,
                                  out FileInfo fileInfo)
        {
            bytesTransferred = default;
            fileInfo         = default;

            return STATUS_MEDIA_WRITE_PROTECTED;
        }

        public override int SetBasicInfo(object fileNode, object fileDesc, uint fileAttributes, ulong creationTime,
                                         ulong lastAccessTime, ulong lastWriteTime, ulong changeTime,
                                         out FileInfo fileInfo)
        {
            fileInfo = default;

            return STATUS_MEDIA_WRITE_PROTECTED;
        }

        public override int SetFileSize(object fileNode, object fileDesc, ulong newSize, bool setAllocationSize,
                                        out FileInfo fileInfo)
        {
            fileInfo = default;

            return STATUS_MEDIA_WRITE_PROTECTED;
        }

        public override int CanDelete(object fileNode, object fileDesc, string fileName) =>
            STATUS_MEDIA_WRITE_PROTECTED;

        public override int Rename(object fileNode, object fileDesc, string fileName, string newFileName,
                                   bool replaceIfExists) => STATUS_MEDIA_WRITE_PROTECTED;

        public override int GetVolumeInfo(out VolumeInfo volumeInfo)
        {
            volumeInfo = new VolumeInfo();

            _vfs.GetInfo(out _, out ulong totalSize);

            volumeInfo.FreeSize  = 0;
            volumeInfo.TotalSize = totalSize;

            return base.GetVolumeInfo(out volumeInfo);
        }
    }
}