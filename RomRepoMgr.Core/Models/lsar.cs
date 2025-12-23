/******************************************************************************
// RomRepoMgr - ROM repository manager
// ----------------------------------------------------------------------------
//
// Author(s)      : Natalia Portillo <claunia@claunia.com>
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
// Copyright © 2020-2026 Natalia Portillo
*******************************************************************************/

using System;
using System.Text.Json.Serialization;

namespace RomRepoMgr.Core.Models;

[JsonSerializable(typeof(lsar))]
public partial class lsarJsonContext : JsonSerializerContext {}

public class lsar
{
    public int            lsarFormatVersion   { get; set; }
    public string         lsarEncoding        { get; set; }
    public int            lsarConfidence      { get; set; }
    public string         lsarFormatName      { get; set; }
    public lsarProperties lsarProperties      { get; set; }
    public lsarContents[] lsarContents        { get; set; }
    public int            lsarError           { get; set; }
    public string         lsarInnerFormatName { get; set; }
    public lsarProperties lsarInnerProperties { get; set; }
}

public class lsarProperties
{
    public string[] XADVolumes              { get; set; }
    public string   XADComment              { get; set; }
    public string   XADArchiveName          { get; set; }
    public bool     XADIsSolid              { get; set; }
    public DateTime XADCreationDate         { get; set; }
    public DateTime XADLastModificationDate { get; set; }
    public string   ARJOriginalArchiveName  { get; set; }
}

public class lsarContents
{
    public long             XADCompressedSize          { get; set; }
    public long             XADDataLength              { get; set; }
    public short            ZipFlags                   { get; set; }
    public short            ZipFileAttributes          { get; set; }
    public long             XADDataOffset              { get; set; }
    public long             XADIndex                   { get; set; }
    public string           ZipOSName                  { get; set; }
    public short            XADPosixPermissions        { get; set; }
    public uint             ZipCRC32                   { get; set; }
    public int              ZipLocalDate               { get; set; }
    public short            ZipOS                      { get; set; }
    public short            ZipCompressionMethod       { get; set; }
    public string           XADCompressionName         { get; set; }
    public short            ZipExtractVersion          { get; set; }
    public string           XADFileName                { get; set; }
    public DateTime         XADLastModificationDate    { get; set; }
    public short            XADDOSFileAttributes       { get; set; }
    public long             XADFileSize                { get; set; }
    public bool             TARIsSparseFile            { get; set; }
    public long             XADFirstSolidEntry         { get; set; }
    public string           XADSolidObject             { get; set; }
    public long             XADFirstSolidIndex         { get; set; }
    public short            XADPosixUser               { get; set; }
    public short            XADPosixGroup              { get; set; }
    public bool             XADIsSolid                 { get; set; }
    public DateTime         XADLastAccessDate          { get; set; }
    public int              ARJCRC32                   { get; set; }
    public int              ARJMethod                  { get; set; }
    public int              ARJMinimumVersion          { get; set; }
    public int              ARJFileType                { get; set; }
    public int              ARJFlags                   { get; set; }
    public string           ARJOSName                  { get; set; }
    public int              ARJOS                      { get; set; }
    public int              ARJVersion                 { get; set; }
    public int              GzipExtraFlags             { get; set; }
    public string           GzipFilename               { get; set; }
    public int              GzipOS                     { get; set; }
    public short            LHAHeaderLevel             { get; set; }
    public string           LHAExtFileNameData         { get; set; }
    public short            LHACRC16                   { get; set; }
    public string           LHAOSName                  { get; set; }
    public int              LHAOS                      { get; set; }
    public string           RAR5OSName                 { get; set; }
    public int              RAR5Attributes             { get; set; }
    public long             RAR5DataLength             { get; set; }
    public long             RAR5CompressionMethod      { get; set; }
    public long             RAR5DictionarySize         { get; set; }
    public uint             RAR5CRC32                  { get; set; }
    public int              RAR5Flags                  { get; set; }
    public int              RAR5DataOffset             { get; set; }
    public int              RAR5CompressionVersion     { get; set; }
    public int              RAR5CompressionInformation { get; set; }
    public int              RAR5OS                     { get; set; }
    public RAR5InputParts[] RAR5InputParts             { get; set; }
}

public class RAR5InputParts
{
    public uint CRC32       { get; set; }
    public long InputLength { get; set; }
    public long Offset      { get; set; }
}