using System;
using System.IO;

namespace RomRepoMgr.Core;

internal sealed class StreamWithLength(Stream baseStream, long length) : Stream
{
    public override bool CanRead  => baseStream.CanRead;
    public override bool CanSeek  => baseStream.CanSeek;
    public override bool CanWrite => baseStream.CanWrite;
    public override long Length   { get; } = length;

    public override long Position
    {
        get => throw new NotSupportedException();
        set => throw new NotSupportedException();
    }

    public override void Flush() => baseStream.Flush();

    public override int Read(byte[] buffer, int offset, int count) => baseStream.Read(buffer, offset, count);

    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

    public override void SetLength(long value) => throw new NotSupportedException();

    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

    public override void Close()
    {
        baseStream.Close();
        base.Close();
    }
}