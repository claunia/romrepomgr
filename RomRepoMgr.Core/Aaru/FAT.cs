// /***************************************************************************
// Aaru Data Preservation Suite
// ----------------------------------------------------------------------------
//
// Filename       : Info.cs
// Author(s)      : Natalia Portillo <claunia@claunia.com>
//
// Component      : Microsoft FAT filesystem plugin.
//
// --[ Description ] ----------------------------------------------------------
//
//     Identifies the Microsoft FAT filesystem and shows information.
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
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Text;

namespace RomRepoMgr.Core.Aaru
{
    // TODO: This should be taken from Aaru as a nuget package in the future
    public static class FAT
    {
        static int CountBits(uint number)
        {
            number -= (number >> 1) & 0x55555555;
            number =  (number & 0x33333333) + ((number >> 2) & 0x33333333);

            return (int)((((number + (number >> 4)) & 0x0F0F0F0F) * 0x01010101) >> 24);
        }

        public static bool Identify(string path)
        {
            try
            {
                return Identify(new FileStream(path, FileMode.Open, FileAccess.Read));
            }
            catch(Exception e)
            {
                return false;
            }
        }

        [SuppressMessage("ReSharper", "JoinDeclarationAndInitializer")]
        static bool Identify(Stream imageStream)
        {
            ushort bps;
            byte   spc;
            byte   numberOfFats;
            ushort reservedSecs;
            ushort rootEntries;
            ushort sectors;
            ushort fatSectors;
            uint   bigSectors;
            byte   bpbSignature;
            byte   fat32Signature;
            ulong  hugeSectors;
            byte[] fat32Id  = new byte[8];
            byte[] msxId    = new byte[6];
            byte[] dosOem   = new byte[8];
            byte[] atariOem = new byte[6];
            ushort bootable = 0;

            byte[] bpbSector = new byte[512];
            byte[] fatSector = new byte[512];
            imageStream.Position = 0;
            imageStream.Read(bpbSector, 0, 512);
            imageStream.Read(fatSector, 0, 512);

            Array.Copy(bpbSector, 0x02, atariOem, 0, 6);
            Array.Copy(bpbSector, 0x03, dosOem, 0, 8);
            bps          = BitConverter.ToUInt16(bpbSector, 0x00B);
            spc          = bpbSector[0x00D];
            reservedSecs = BitConverter.ToUInt16(bpbSector, 0x00E);
            numberOfFats = bpbSector[0x010];
            rootEntries  = BitConverter.ToUInt16(bpbSector, 0x011);
            sectors      = BitConverter.ToUInt16(bpbSector, 0x013);
            fatSectors   = BitConverter.ToUInt16(bpbSector, 0x016);
            Array.Copy(bpbSector, 0x052, msxId, 0, 6);
            bigSectors     = BitConverter.ToUInt32(bpbSector, 0x020);
            bpbSignature   = bpbSector[0x026];
            fat32Signature = bpbSector[0x042];
            Array.Copy(bpbSector, 0x052, fat32Id, 0, 8);
            hugeSectors = BitConverter.ToUInt64(bpbSector, 0x052);
            int bitsInBps = CountBits(bps);

            bootable = BitConverter.ToUInt16(bpbSector, 0x1FE);

            bool   correctSpc  = spc == 1 || spc == 2 || spc == 4 || spc == 8 || spc == 16 || spc == 32 || spc == 64;
            string msxString   = Encoding.ASCII.GetString(msxId);
            string fat32String = Encoding.ASCII.GetString(fat32Id);

            string oemString = Encoding.ASCII.GetString(dosOem);

            ushort apricotBps          = BitConverter.ToUInt16(bpbSector, 0x50);
            byte   apricotSpc          = bpbSector[0x52];
            ushort apricotReservedSecs = BitConverter.ToUInt16(bpbSector, 0x53);
            byte   apricotFatsNo       = bpbSector[0x55];
            ushort apricotRootEntries  = BitConverter.ToUInt16(bpbSector, 0x56);
            ushort apricotSectors      = BitConverter.ToUInt16(bpbSector, 0x58);
            ushort apricotFatSectors   = BitConverter.ToUInt16(bpbSector, 0x5B);

            bool apricotCorrectSpc = apricotSpc == 1  || apricotSpc == 2  || apricotSpc == 4 || apricotSpc == 8 ||
                                     apricotSpc == 16 || apricotSpc == 32 || apricotSpc == 64;

            int  bitsInApricotBps  = CountBits(apricotBps);
            byte apricotPartitions = bpbSector[0x0C];

            switch(oemString)
            {
                // exFAT
                case "EXFAT   ": return false;

                // NTFS
                case "NTFS    " when bootable == 0xAA55 && numberOfFats == 0 && fatSectors == 0: return false;

                // QNX4
                case "FQNX4FS ": return false;
            }

            ulong imageSectors = (ulong)imageStream.Length / 512;

            switch(bitsInBps)
            {
                // FAT32 for sure
                case 1 when correctSpc && numberOfFats <= 2    && sectors     == 0 && fatSectors == 0 &&
                            fat32Signature             == 0x29 && fat32String == "FAT32   ": return true;

                // short FAT32
                case 1
                    when correctSpc && numberOfFats <= 2 && sectors == 0 && fatSectors == 0 && fat32Signature == 0x28:
                    return bigSectors == 0 ? hugeSectors <= imageSectors : bigSectors <= imageSectors;

                // MSX-DOS FAT12
                case 1 when correctSpc && numberOfFats <= 2 && rootEntries > 0 && sectors <= imageSectors &&
                            fatSectors                 > 0  && msxString   == "VOL_ID": return true;

                // EBPB
                case 1 when correctSpc && numberOfFats <= 2 && rootEntries > 0 && fatSectors > 0 &&
                            (bpbSignature == 0x28 || bpbSignature == 0x29):
                    return sectors == 0 ? bigSectors <= imageSectors : sectors <= imageSectors;

                // BPB
                case 1 when correctSpc && reservedSecs < imageSectors - 1 && numberOfFats <= 2 && rootEntries > 0 &&
                            fatSectors > 0: return sectors == 0 ? bigSectors <= imageSectors : sectors <= imageSectors;
            }

            // Apricot BPB
            if(bitsInApricotBps == 1                  &&
               apricotCorrectSpc                      &&
               apricotReservedSecs < imageSectors - 1 &&
               apricotFatsNo       <= 2               &&
               apricotRootEntries  > 0                &&
               apricotFatSectors   > 0                &&
               apricotSectors      <= imageSectors    &&
               apricotPartitions   == 0)
                return true;

            // DEC Rainbow, lacks a BPB but has a very concrete structure...
            if(imageSectors != 800)
                return false;

            // DEC Rainbow boots up with a Z80, first byte should be DI (disable interrupts)
            byte z80Di = bpbSector[0];

            // First FAT1 sector resides at LBA 0x14
            byte[] fat1Sector0 = new byte[512];
            imageStream.Position = 0x14 * 512;
            imageStream.Read(fat1Sector0, 0, 512);

            // First FAT2 sector resides at LBA 0x1A
            byte[] fat2Sector0 = new byte[512];
            imageStream.Position = 0x1A * 512;
            imageStream.Read(fat2Sector0, 0, 512);
            bool equalFatIds = fat1Sector0[0] == fat2Sector0[0] && fat1Sector0[1] == fat2Sector0[1];

            // Volume is software interleaved 2:1
            var rootMs = new MemoryStream();

            byte[] tmp = new byte[512];

            foreach(long position in new long[]
            {
                0x17, 0x19, 0x1B, 0x1D, 0x1E, 0x20
            })
            {
                imageStream.Position = position * 512;
                imageStream.Read(tmp, 0, 512);
                rootMs.Write(tmp, 0, tmp.Length);
            }

            byte[] rootDir      = rootMs.ToArray();
            bool   validRootDir = true;

            // Iterate all root directory
            for(int e = 0; e < 96 * 32; e += 32)
            {
                for(int c = 0; c < 11; c++)
                    if((rootDir[c + e] < 0x20 && rootDir[c + e] != 0x00 && rootDir[c + e] != 0x05) ||
                       rootDir[c + e] == 0xFF                                                      ||
                       rootDir[c + e] == 0x2E)
                    {
                        validRootDir = false;

                        break;
                    }

                if(!validRootDir)
                    break;
            }

            return z80Di == 0xF3 && equalFatIds && (fat1Sector0[0] & 0xF0) == 0xF0 && fat1Sector0[1] == 0xFF &&
                   validRootDir;
        }
    }
}