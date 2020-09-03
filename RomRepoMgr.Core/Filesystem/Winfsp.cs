using System;
using System.Collections.Concurrent;
using System.IO;
using System.Runtime.InteropServices;
using Fsp;
using Fsp.Interop;
using RomRepoMgr.Database.Models;
using FileInfo = Fsp.Interop.FileInfo;

namespace RomRepoMgr.Core.Filesystem
{
    public class Winfsp : FileSystemBase
    {
        readonly ConcurrentDictionary<long, FileInfo> _fileStatHandleCache;
        readonly Vfs                                  _vfs;
        FileSystemHost                                _host;

        public Winfsp(Vfs vfs)
        {
            _vfs                 = vfs;
            _fileStatHandleCache = new ConcurrentDictionary<long, FileInfo>();
        }

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

        public override int Open(string fileName, uint createOptions, uint grantedAccess, out object fileNode,
                                 out object fileDesc, out FileInfo fileInfo, out string normalizedName)
        {
            fileNode       = default;
            fileDesc       = default;
            fileInfo       = default;
            normalizedName = default;

            string[] pieces = _vfs.SplitPath(fileName);

            if(pieces.Length == 0)
                return STATUS_FILE_IS_A_DIRECTORY;

            long romSetId = _vfs.GetRomSetId(pieces[0]);

            if(romSetId <= 0)
                return STATUS_OBJECT_NAME_NOT_FOUND;

            RomSet romSet = _vfs.GetRomSet(romSetId);

            if(romSet == null)
                return STATUS_OBJECT_NAME_NOT_FOUND;

            if(pieces.Length == 1)
                return STATUS_FILE_IS_A_DIRECTORY;

            CachedMachine machine = _vfs.GetMachine(romSetId, pieces[1]);

            if(machine == null)
                return STATUS_OBJECT_NAME_NOT_FOUND;

            if(pieces.Length == 2)
                return STATUS_FILE_IS_A_DIRECTORY;

            CachedFile file = _vfs.GetFile(machine.Id, pieces[2]);

            if(file == null)
                return STATUS_OBJECT_NAME_NOT_FOUND;

            if(pieces.Length > 3)
                return STATUS_FILE_IS_A_DIRECTORY;

            if(file.Sha384 == null)
                return STATUS_OBJECT_NAME_NOT_FOUND;

            long handle = _vfs.Open(file.Sha384, (long)file.Size);

            if(handle <= 0)
                return STATUS_OBJECT_NAME_NOT_FOUND;

            fileNode       = handle;
            normalizedName = Path.GetFileName(fileName);

            // TODO: Real allocation size
            fileInfo = new FileInfo
            {
                ChangeTime     = (ulong)file.UpdatedOn.ToFileTimeUtc(),
                AllocationSize = (file.Size + 511) / 512,
                FileSize       = file.Size,
                CreationTime   = (ulong)file.CreatedOn.ToFileTimeUtc(),
                FileAttributes = (uint)(FileAttributes.Normal | FileAttributes.Compressed | FileAttributes.ReadOnly),
                IndexNumber    = file.Id,
                LastAccessTime = (ulong)DateTime.UtcNow.ToFileTimeUtc(),
                LastWriteTime  = (ulong)file.UpdatedOn.ToFileTimeUtc()
            };

            _fileStatHandleCache[handle] = fileInfo;

            return STATUS_SUCCESS;
        }

        public override void Close(object fileNode, object fileDesc)
        {
            if(!(fileNode is long handle))
                return;

            _vfs.Close(handle);
            _fileStatHandleCache.TryRemove(handle, out _);
        }

        public override int Read(object fileNode, object fileDesc, IntPtr buffer, ulong offset, uint length,
                                 out uint bytesTransferred)
        {
            bytesTransferred = 0;

            if(!(fileNode is long handle))
                return STATUS_INVALID_HANDLE;

            byte[] buf = new byte[length];

            int ret = _vfs.Read(handle, buf, (long)offset);

            if(ret < 0)
                return STATUS_INVALID_HANDLE;

            Marshal.Copy(buf, 0, buffer, ret);

            bytesTransferred = (uint)ret;

            return STATUS_SUCCESS;
        }

        public override int GetFileInfo(object fileNode, object fileDesc, out FileInfo fileInfo)
        {
            fileInfo = default;

            if(!(fileNode is long handle))
                return STATUS_INVALID_HANDLE;

            if(!_fileStatHandleCache.TryGetValue(handle, out FileInfo info))
                return STATUS_INVALID_HANDLE;

            fileInfo = info;

            return STATUS_SUCCESS;
        }
    }
}