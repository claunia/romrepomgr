using System;
using Fsp;

namespace RomRepoMgr.Core.Filesystem
{
    public class Winfsp : FileSystemBase
    {
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
    }
}