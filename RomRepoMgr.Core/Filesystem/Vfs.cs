namespace RomRepoMgr.Core.Filesystem
{
    public class Vfs
    {
        public static bool IsAvailable => Winfsp.IsAvailable || Fuse.IsAvailable;
    }
}