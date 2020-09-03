using System;
using System.Collections.Generic;
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

        public override int Open(string fileName, uint createOptions, uint grantedAccess, out object fileNode,
                                 out object fileDesc, out FileInfo fileInfo, out string normalizedName)
        {
            fileNode       = default;
            fileDesc       = default;
            fileInfo       = default;
            normalizedName = default;

            string[] pieces = _vfs.SplitPath(fileName);

            // Root directory
            if(pieces.Length == 0)
            {
                fileInfo = new FileInfo
                {
                    CreationTime   = (ulong)DateTime.UtcNow.ToFileTimeUtc(),
                    FileAttributes = (uint)(FileAttributes.Directory | FileAttributes.Compressed),
                    LastWriteTime  = (ulong)DateTime.UtcNow.ToFileTimeUtc()
                };

                normalizedName = "";

                fileNode = new FileNode
                {
                    FileName    = normalizedName,
                    IsDirectory = true,
                    Info        = fileInfo,
                    Path        = fileName
                };

                return STATUS_SUCCESS;
            }

            long romSetId = _vfs.GetRomSetId(pieces[0]);

            if(romSetId <= 0)
                return STATUS_OBJECT_NAME_NOT_FOUND;

            RomSet romSet = _vfs.GetRomSet(romSetId);

            if(romSet == null)
                return STATUS_OBJECT_NAME_NOT_FOUND;

            // ROM Set
            if(pieces.Length == 1)
            {
                fileInfo = new FileInfo
                {
                    CreationTime   = (ulong)romSet.CreatedOn.ToUniversalTime().ToFileTimeUtc(),
                    FileAttributes = (uint)(FileAttributes.Directory | FileAttributes.Compressed),
                    LastWriteTime  = (ulong)romSet.UpdatedOn.ToUniversalTime().ToFileTimeUtc()
                };

                normalizedName = Path.GetFileName(fileName);

                fileNode = new FileNode
                {
                    FileName    = normalizedName,
                    IsDirectory = true,
                    Info        = fileInfo,
                    Path        = fileName,
                    RomSetId    = romSet.Id
                };

                return STATUS_SUCCESS;
            }

            CachedMachine machine = _vfs.GetMachine(romSetId, pieces[1]);

            if(machine == null)
                return STATUS_OBJECT_NAME_NOT_FOUND;

            // Machine
            if(pieces.Length == 2)
            {
                fileInfo = new FileInfo
                {
                    CreationTime   = (ulong)romSet.CreatedOn.ToUniversalTime().ToFileTimeUtc(),
                    FileAttributes = (uint)(FileAttributes.Directory | FileAttributes.Compressed),
                    LastWriteTime  = (ulong)romSet.UpdatedOn.ToUniversalTime().ToFileTimeUtc()
                };

                normalizedName = Path.GetFileName(fileName);

                fileNode = new FileNode
                {
                    FileName    = normalizedName,
                    IsDirectory = true,
                    Info        = fileInfo,
                    Path        = fileName,
                    MachineId   = machine.Id
                };

                return STATUS_SUCCESS;
            }

            CachedFile file = _vfs.GetFile(machine.Id, pieces[2]);

            if(file == null)
                return STATUS_OBJECT_NAME_NOT_FOUND;

            if(pieces.Length > 3)
                return STATUS_INVALID_DEVICE_REQUEST;

            if(file.Sha384 == null)
                return STATUS_OBJECT_NAME_NOT_FOUND;

            long handle = _vfs.Open(file.Sha384, (long)file.Size);

            if(handle <= 0)
                return STATUS_OBJECT_NAME_NOT_FOUND;

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

            fileNode = new FileNode
            {
                FileName = normalizedName,
                Info     = fileInfo,
                Path     = fileName,
                Handle   = handle
            };

            return STATUS_SUCCESS;
        }

        public override void Close(object fileNode, object fileDesc)
        {
            if(!(fileNode is FileNode node))
                return;

            if(node.Handle <= 0)
                return;

            _vfs.Close(node.Handle);
        }

        public override int Read(object fileNode, object fileDesc, IntPtr buffer, ulong offset, uint length,
                                 out uint bytesTransferred)
        {
            bytesTransferred = 0;

            if(!(fileNode is FileNode node) ||
               node.Handle <= 0)
                return STATUS_INVALID_HANDLE;

            byte[] buf = new byte[length];

            int ret = _vfs.Read(node.Handle, buf, (long)offset);

            if(ret < 0)
                return STATUS_INVALID_HANDLE;

            Marshal.Copy(buf, 0, buffer, ret);

            bytesTransferred = (uint)ret;

            return STATUS_SUCCESS;
        }

        public override int GetFileInfo(object fileNode, object fileDesc, out FileInfo fileInfo)
        {
            fileInfo = default;

            if(!(fileNode is FileNode node))
                return STATUS_INVALID_HANDLE;

            fileInfo = node.Info;

            return STATUS_SUCCESS;
        }

        class FileNode
        {
            public FileInfo     Info        { get; set; }
            public string       FileName    { get; set; }
            public string       Path        { get; set; }
            public long         Handle      { get; set; }
            public List<string> Children    { get; set; }
            public bool         IsDirectory { get; set; }
            public long         RomSetId    { get; set; }
            public ulong        MachineId   { get; set; }
        }
    }
}