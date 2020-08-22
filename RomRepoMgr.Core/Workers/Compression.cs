using System;
using System.IO;
using RomRepoMgr.Core.EventArgs;
using SharpCompress.Compressors;
using SharpCompress.Compressors.LZMA;

namespace RomRepoMgr.Core.Workers
{
    public sealed class Compression
    {
        const long BUFFER_SIZE = 131072;

        public event EventHandler<ProgressBoundsEventArgs> SetProgressBounds;
        public event EventHandler<ProgressEventArgs>       SetProgress;

        public void CompressFile(string source, string destination)
        {
            var    inFs    = new FileStream(source, FileMode.Open, FileAccess.Read);
            var    outFs   = new FileStream(destination, FileMode.CreateNew, FileAccess.Write);
            Stream zStream = new LZipStream(outFs, CompressionMode.Compress);

            byte[] buffer = new byte[BUFFER_SIZE];

            SetProgressBounds?.Invoke(this, new ProgressBoundsEventArgs
            {
                Minimum = 0,
                Maximum = inFs.Length
            });

            while(inFs.Position + BUFFER_SIZE <= inFs.Length)
            {
                SetProgress?.Invoke(this, new ProgressEventArgs
                {
                    Value = inFs.Position
                });

                inFs.Read(buffer, 0, buffer.Length);
                zStream.Write(buffer, 0, buffer.Length);
            }

            buffer = new byte[inFs.Length - inFs.Position];

            SetProgressBounds?.Invoke(this, new ProgressBoundsEventArgs
            {
                Minimum = 0,
                Maximum = inFs.Length
            });

            inFs.Read(buffer, 0, buffer.Length);
            zStream.Write(buffer, 0, buffer.Length);

            inFs.Close();
            zStream.Close();
            outFs.Dispose();
        }
    }
}