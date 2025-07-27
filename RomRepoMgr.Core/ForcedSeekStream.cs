// /***************************************************************************
// Aaru Data Preservation Suite
// ----------------------------------------------------------------------------
//
// Filename       : ForcedSeekStream.cs
// Author(s)      : Natalia Portillo <claunia@claunia.com>
//
// Component      : Filters.
//
// --[ Description ] ----------------------------------------------------------
//
//     Provides a seekable stream from a forward-readable stream.
//
// --[ License ] --------------------------------------------------------------
//
//     This library is free software; you can redistribute it and/or modify
//     it under the terms of the GNU Lesser General Public License as
//     published by the Free Software Foundation; either version 2.1 of the
//     License, or (at your option) any later version.
//
//     This library is distributed in the hope that it will be useful, but
//     WITHOUT ANY WARRANTY; without even the implied warranty of
//     MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the GNU
//     Lesser General Public License for more details.
//
//     You should have received a copy of the GNU Lesser General Public
//     License along with this library; if not, see <http://www.gnu.org/licenses/>.
//
// ----------------------------------------------------------------------------
// Copyright © 2011-2024 Natalia Portillo
// ****************************************************************************/

using System;
using System.IO;
using RomRepoMgr.Core.Resources;

namespace RomRepoMgr.Core;

/// <inheritdoc />
/// <summary>
///     ForcedSeekStream allows to seek a forward-readable stream (like System.IO.Compression streams) by doing the
///     slow and known trick of rewinding and forward reading until arriving the desired position.
/// </summary>
internal sealed class ForcedSeekStream<T> : Stream where T : Stream
{
    const    int        BUFFER_LEN = 1048576;
    readonly string     _backFile;
    readonly FileStream _backStream;
    readonly T          _baseStream;
    long                _streamLength;

    /// <inheritdoc />
    /// <summary>Initializes a new instance of the <see cref="T:RomRepoMgr.Core.ForcedSeekStream`1" /> class.</summary>
    /// <param name="length">The real (uncompressed) length of the stream.</param>
    /// <param name="args">Parameters that are used to create the base stream.</param>
    public ForcedSeekStream(long length, params object[] args)
    {
        _streamLength = length;
        _baseStream   = (T)Activator.CreateInstance(typeof(T), args);
        _backFile     = Path.GetTempFileName();
        _backStream   = new FileStream(_backFile, FileMode.Open, FileAccess.ReadWrite, FileShare.None);

        if(length == 0) CalculateLength();
    }

    public override bool CanRead => _baseStream.CanRead;

    public override bool CanSeek => true;

    public override bool CanWrite => false;

    public override long Length => _streamLength;

    public override long Position
    {
        get => _backStream.Position;

        set => SetPosition(value);
    }

    /// <summary>
    ///     Calculates the real (uncompressed) length of the stream. It basically reads (decompresses) the whole stream to
    ///     memory discarding its contents, so it should be used as a last resort.
    /// </summary>
    /// <returns>The length.</returns>
    void CalculateLength()
    {
        int read;

        do
        {
            byte[] buffer = new byte[BUFFER_LEN];
            read = _baseStream.Read(buffer, 0, BUFFER_LEN);
            _backStream.Write(buffer, 0, read);
        } while(read == BUFFER_LEN);

        _streamLength        = _backStream.Length;
        _backStream.Position = 0;
    }

    void SetPosition(long position)
    {
        if(position == _backStream.Position) return;

        if(position < _backStream.Length)
        {
            _backStream.Position = position;

            return;
        }

        _backStream.Position = _backStream.Length;
        long   toPosition      = position - _backStream.Position;
        int    fullBufferReads = (int)(toPosition / BUFFER_LEN);
        int    restToRead      = (int)(toPosition % BUFFER_LEN);
        byte[] buffer;

        for(int i = 0; i < fullBufferReads; i++)
        {
            buffer = new byte[BUFFER_LEN];
            _baseStream.ReadExactly(buffer, 0, BUFFER_LEN);
            _backStream.Write(buffer, 0, BUFFER_LEN);
        }

        buffer = new byte[restToRead];
        _baseStream.ReadExactly(buffer, 0, restToRead);
        _backStream.Write(buffer, 0, restToRead);
    }

    public override void Flush()
    {
        _baseStream.Flush();
        _backStream.Flush();
    }

    public override int Read(byte[] buffer, int offset, int count)
    {
        if(_backStream.Position + count <= _backStream.Length) return _backStream.Read(buffer, offset, count);

        SetPosition(_backStream.Position + count);
        SetPosition(_backStream.Position - count);

        return _backStream.Read(buffer, offset, count);
    }

    public override int ReadByte()
    {
        if(_backStream.Position + 1 <= _backStream.Length) return _backStream.ReadByte();

        SetPosition(_backStream.Position + 1);
        SetPosition(_backStream.Position - 1);

        return _backStream.ReadByte();
    }

    public override long Seek(long offset, SeekOrigin origin)
    {
        switch(origin)
        {
            case SeekOrigin.Begin:
                if(offset < 0) throw new IOException(Localization.Cannot_seek_before_start);

                SetPosition(offset);

                break;
            case SeekOrigin.End:
                if(offset > 0) throw new IOException(Localization.Cannot_seek_after_end);

                if(_streamLength == 0) CalculateLength();

                SetPosition(_streamLength + offset);

                break;
            default:
                SetPosition(_backStream.Position + offset);

                break;
        }

        return _backStream.Position;
    }

    public override void SetLength(long value) => throw new NotSupportedException();

    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

    public override void Close()
    {
        _backStream?.Close();
        File.Delete(_backFile);
    }

    ~ForcedSeekStream()
    {
        _backStream?.Close();
        File.Delete(_backFile);
    }
}