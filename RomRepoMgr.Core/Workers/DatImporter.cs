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
// Copyright © 2020 Natalia Portillo
*******************************************************************************/

using System;
using System.Diagnostics;
using System.IO;
using Aaru.Checksums;
using RomRepoMgr.Core.EventArgs;
using RomRepoMgr.Core.Models;
using RomRepoMgr.Database;
using RomRepoMgr.Database.Models;
using SabreTools.Library.DatFiles;
using ErrorEventArgs = RomRepoMgr.Core.EventArgs.ErrorEventArgs;

namespace RomRepoMgr.Core.Workers
{
    public sealed class DatImporter
    {
        readonly string _datFilesPath;
        readonly string _datPath;
        bool            _aborted;

        public DatImporter(string datPath)
        {
            _datPath      = datPath;
            _datFilesPath = Path.Combine(Settings.Settings.Current.RepositoryPath, "datfiles");
        }

        public void Import()
        {
            try
            {
                SetIndeterminateProgress?.Invoke(this, System.EventArgs.Empty);

                SetMessage?.Invoke(this, new MessageEventArgs
                {
                    Message = "Parsing DAT file..."
                });

                DateTime start   = DateTime.UtcNow;
                var      datFile = DatFile.CreateAndParse(_datPath);
                DateTime end     = DateTime.UtcNow;
                double   elapsed = (end - start).TotalSeconds;

                SetMessage?.Invoke(this, new MessageEventArgs
                {
                    Message = "Hashing DAT file..."
                });

                string datHash = Sha384Context.File(_datPath, out byte[] datHashBinary);

                string datHash32 = Base32.ToBase32String(datHashBinary);

                if(!Directory.Exists(_datFilesPath))
                    Directory.CreateDirectory(_datFilesPath);

                string compressedDatPath = Path.Combine(_datFilesPath, datHash32 + ".lz");

                if(File.Exists(compressedDatPath))
                {
                    ErrorOccurred?.Invoke(this, new ErrorEventArgs
                    {
                        Message = "DAT file is already in database, not importing duplicates."
                    });

                    return;
                }

                SetMessage?.Invoke(this, new MessageEventArgs
                {
                    Message = "Adding DAT to database..."
                });

                // TODO: Check if there is a hash in database but not in repo

                var romSet = new RomSet
                {
                    Author      = datFile.Header.Author,
                    Comment     = datFile.Header.Comment,
                    Date        = datFile.Header.Date,
                    Description = datFile.Header.Description,
                    Filename    = Path.GetFileName(_datPath),
                    Homepage    = datFile.Header.Homepage,
                    Name        = datFile.Header.Name,
                    Sha384      = datHash,
                    Version     = datFile.Header.Version,
                    CreatedOn   = DateTime.UtcNow,
                    UpdatedOn   = DateTime.UtcNow
                };

                Context.Singleton.RomSets.Add(romSet);
                Context.Singleton.SaveChanges();

                RomSetAdded?.Invoke(this, new RomSetEventArgs
                {
                    RomSet = new RomSetModel
                    {
                        Author      = romSet.Author,
                        Comment     = romSet.Comment,
                        Date        = romSet.Date,
                        Description = romSet.Description,
                        Filename    = romSet.Filename,
                        Homepage    = romSet.Homepage,
                        Name        = romSet.Name,
                        Sha384      = romSet.Sha384,
                        Version     = romSet.Version
                    }
                });

                SetMessage?.Invoke(this, new MessageEventArgs
                {
                    Message = "Compressing DAT file..."
                });

                var datCompress = new Compression();
                datCompress.SetProgress       += SetProgress;
                datCompress.SetProgressBounds += SetProgressBounds;
                datCompress.CompressFile(_datPath, compressedDatPath);

                WorkFinished?.Invoke(this, System.EventArgs.Empty);
            }
            catch(Exception e)
            {
                if(Debugger.IsAttached)
                    throw;

                ErrorOccurred?.Invoke(this, new ErrorEventArgs
                {
                    Message = "Unhandled exception occurred."
                });
            }
        }

        public void Abort() => _aborted = true;

        public event EventHandler                          SetIndeterminateProgress;
        public event EventHandler                          WorkFinished;
        public event EventHandler<ErrorEventArgs>          ErrorOccurred;
        public event EventHandler<ProgressBoundsEventArgs> SetProgressBounds;
        public event EventHandler<ProgressEventArgs>       SetProgress;
        public event EventHandler<MessageEventArgs>        SetMessage;
        public event EventHandler<RomSetEventArgs>         RomSetAdded;
    }
}