using System;
using System.IO;

namespace RomRepoMgr.Core;

internal sealed class StreamWithLength : Stream
{
    readonly Stream _baseStream;

    public StreamWithLength(Stream baseStream, long length)
    {
        _baseStream = baseStream;
        Length      = length;
    }

    public override bool CanRead  => _baseStream.CanRead;
    public override bool CanSeek  => _baseStream.CanSeek;
    public override bool CanWrite => _baseStream.CanWrite;
    public override long Length   { get; }

    public override long Position
    {
        get => throw new NotSupportedException();
        set => throw new NotSupportedException();
    }

    public override void Flush() => _baseStream.Flush();

    public override int Read(byte[] buffer, int offset, int count) => _baseStream.Read(buffer, offset, count);

    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

    public override void SetLength(long value) => throw new NotSupportedException();

    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

    public override void Close()
    {
        _baseStream.Close();
        base.Close();
    }
}