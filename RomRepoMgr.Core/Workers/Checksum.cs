// /***************************************************************************
// The Disc Image Chef
// ----------------------------------------------------------------------------
//
// Filename       : Checksum.cs
// Author(s)      : Natalia Portillo <claunia@claunia.com>
//
// Component      : Core methods.
//
// --[ Description ] ----------------------------------------------------------
//
//     Methods to checksum data.
//
// --[ License ] --------------------------------------------------------------
//
//     This program is free software: you can redistribute it and/or modify
//     it under the terms of the GNU General Public License as
//     published by the Free Software Foundation, either version 3 of the
//     License, or (at your option) any later version.
//
//     This program is distributed in the hope that it will be useful,
//     but WITHOUT ANY WARRANTY; without even the implied warranty of
//     MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
//     GNU General Public License for more details.
//
//     You should have received a copy of the GNU General Public License
//     along with this program.  If not, see <http://www.gnu.org/licenses/>.
//
// ----------------------------------------------------------------------------
// Copyright © 2011-2024 Natalia Portillo
// ****************************************************************************/

using System.Collections.Generic;
using System.Threading;
using Aaru.Checksums;

namespace RomRepoMgr.Core.Workers
{
    internal enum ChecksumType
    {
        Crc32, Md5, Sha1,
        Sha256, Sha384, Sha512
    }

    internal sealed class Checksum
    {
        readonly Crc32Context  _crc32Ctx;
        readonly Md5Context    _md5Ctx;
        readonly Sha1Context   _sha1Ctx;
        readonly Sha256Context _sha256Ctx;
        readonly Sha384Context _sha384Ctx;
        readonly Sha512Context _sha512Ctx;
        Crc32Packet            _crc32Pkt;
        Thread                 _crc32Thread;
        Md5Packet              _md5Pkt;
        Thread                 _md5Thread;
        Sha1Packet             _sha1Pkt;
        Thread                 _sha1Thread;
        Sha256Packet           _sha256Pkt;
        Thread                 _sha256Thread;
        Sha384Packet           _sha384Pkt;
        Thread                 _sha384Thread;
        Sha512Packet           _sha512Pkt;
        Thread                 _sha512Thread;

        internal Checksum()
        {
            _crc32Ctx  = new Crc32Context();
            _md5Ctx    = new Md5Context();
            _sha1Ctx   = new Sha1Context();
            _sha256Ctx = new Sha256Context();
            _sha384Ctx = new Sha384Context();
            _sha512Ctx = new Sha512Context();

            _crc32Thread  = new Thread(UpdateCrc32);
            _md5Thread    = new Thread(UpdateMd5);
            _sha1Thread   = new Thread(UpdateSha1);
            _sha256Thread = new Thread(UpdateSha256);
            _sha384Thread = new Thread(UpdateSha384);
            _sha512Thread = new Thread(UpdateSha512);

            _crc32Pkt  = new Crc32Packet();
            _md5Pkt    = new Md5Packet();
            _sha1Pkt   = new Sha1Packet();
            _sha256Pkt = new Sha256Packet();
            _sha384Pkt = new Sha384Packet();
            _sha512Pkt = new Sha512Packet();

            _crc32Pkt.context  = _crc32Ctx;
            _md5Pkt.context    = _md5Ctx;
            _sha1Pkt.context   = _sha1Ctx;
            _sha256Pkt.context = _sha256Ctx;
            _sha384Pkt.context = _sha384Ctx;
            _sha512Pkt.context = _sha512Ctx;
        }

        internal void Update(byte[] data)
        {
            _crc32Pkt.data = data;
            _crc32Thread.Start(_crc32Pkt);
            _md5Pkt.data = data;
            _md5Thread.Start(_md5Pkt);
            _sha1Pkt.data = data;
            _sha1Thread.Start(_sha1Pkt);
            _sha256Pkt.data = data;
            _sha256Thread.Start(_sha256Pkt);
            _sha384Pkt.data = data;
            _sha384Thread.Start(_sha384Pkt);
            _sha512Pkt.data = data;
            _sha512Thread.Start(_sha512Pkt);

            while(_crc32Thread.IsAlive  ||
                  _md5Thread.IsAlive    ||
                  _sha1Thread.IsAlive   ||
                  _sha256Thread.IsAlive ||
                  _sha384Thread.IsAlive ||
                  _sha512Thread.IsAlive) {}

            _crc32Thread  = new Thread(UpdateCrc32);
            _md5Thread    = new Thread(UpdateMd5);
            _sha1Thread   = new Thread(UpdateSha1);
            _sha256Thread = new Thread(UpdateSha256);
            _sha384Thread = new Thread(UpdateSha384);
            _sha512Thread = new Thread(UpdateSha512);
        }

        internal Dictionary<ChecksumType, string> End() => new Dictionary<ChecksumType, string>
        {
            [ChecksumType.Crc32]  = _crc32Ctx.End(),
            [ChecksumType.Md5]    = _md5Ctx.End(),
            [ChecksumType.Sha1]   = _sha1Ctx.End(),
            [ChecksumType.Sha256] = _sha256Ctx.End(),
            [ChecksumType.Sha384] = _sha384Ctx.End(),
            [ChecksumType.Sha512] = _sha512Ctx.End()
        };

        #region Threading helpers
        struct Crc32Packet
        {
            public Crc32Context context;
            public byte[]       data;
        }

        struct Md5Packet
        {
            public Md5Context context;
            public byte[]     data;
        }

        struct Sha1Packet
        {
            public Sha1Context context;
            public byte[]      data;
        }

        struct Sha256Packet
        {
            public Sha256Context context;
            public byte[]        data;
        }

        struct Sha384Packet
        {
            public Sha384Context context;
            public byte[]        data;
        }

        struct Sha512Packet
        {
            public Sha512Context context;
            public byte[]        data;
        }

        static void UpdateCrc32(object packet) => ((Crc32Packet)packet).context.Update(((Crc32Packet)packet).data);

        static void UpdateMd5(object packet) => ((Md5Packet)packet).context.Update(((Md5Packet)packet).data);

        static void UpdateSha1(object packet) => ((Sha1Packet)packet).context.Update(((Sha1Packet)packet).data);

        static void UpdateSha256(object packet) => ((Sha256Packet)packet).context.Update(((Sha256Packet)packet).data);

        static void UpdateSha384(object packet) => ((Sha384Packet)packet).context.Update(((Sha384Packet)packet).data);

        static void UpdateSha512(object packet) => ((Sha512Packet)packet).context.Update(((Sha512Packet)packet).data);
        #endregion Threading helpers
    }
}