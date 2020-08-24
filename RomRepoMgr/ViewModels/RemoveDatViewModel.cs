﻿/******************************************************************************
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

using System.IO;
using System.Threading.Tasks;
using Avalonia.Threading;
using JetBrains.Annotations;
using ReactiveUI;
using RomRepoMgr.Core;
using RomRepoMgr.Database;
using RomRepoMgr.Database.Models;
using RomRepoMgr.Views;

namespace RomRepoMgr.ViewModels
{
    public sealed class RemoveDatViewModel : ViewModelBase
    {
        readonly long      _romSetId;
        readonly RemoveDat _view;
        string             _statusMessage;

        public RemoveDatViewModel(RemoveDat view, long romSetId)
        {
            _view     = view;
            _romSetId = romSetId;
        }

        [NotNull]
        public string Title => "Removing ROM set...";

        public string StatusMessage
        {
            get => _statusMessage;
            set => this.RaiseAndSetIfChanged(ref _statusMessage, value);
        }

        internal void OnOpened() => Task.Run(() =>
        {
            Dispatcher.UIThread.Post(() => StatusMessage = "Retrieving ROM set from database...");

            RomSet romSet = Context.Singleton.RomSets.Find(_romSetId);

            if(romSet == null)
                return;

            Dispatcher.UIThread.Post(() => StatusMessage = "Removing ROM set from database...");

            Context.Singleton.RomSets.Remove(romSet);

            Dispatcher.UIThread.Post(() => StatusMessage = "Saving changes to database...");

            Context.Singleton.SaveChanges();

            Dispatcher.UIThread.Post(() => StatusMessage = "Removing DAT file from repo...");

            byte[] sha384Bytes = new byte[48];
            string sha384      = romSet.Sha384;

            for(int i = 0; i < 48; i++)
            {
                if(sha384[i * 2] >= 0x30 &&
                   sha384[i * 2] <= 0x39)
                    sha384Bytes[i] = (byte)((sha384[i * 2] - 0x30) * 0x10);
                else if(sha384[i * 2] >= 0x41 &&
                        sha384[i * 2] <= 0x46)
                    sha384Bytes[i] = (byte)((sha384[i * 2] - 0x37) * 0x10);
                else if(sha384[i * 2] >= 0x61 &&
                        sha384[i * 2] <= 0x66)
                    sha384Bytes[i] = (byte)((sha384[i * 2] - 0x57) * 0x10);

                if(sha384[(i * 2) + 1] >= 0x30 &&
                   sha384[(i * 2) + 1] <= 0x39)
                    sha384Bytes[i] += (byte)(sha384[(i * 2) + 1] - 0x30);
                else if(sha384[(i * 2) + 1] >= 0x41 &&
                        sha384[(i * 2) + 1] <= 0x46)
                    sha384Bytes[i] += (byte)(sha384[(i * 2) + 1] - 0x37);
                else if(sha384[(i * 2) + 1] >= 0x61 &&
                        sha384[(i * 2) + 1] <= 0x66)
                    sha384Bytes[i] += (byte)(sha384[(i * 2) + 1] - 0x57);
            }

            string datHash32         = Base32.ToBase32String(sha384Bytes);
            string datFilesPath      = Path.Combine(Settings.Settings.Current.RepositoryPath, "datfiles");
            string compressedDatPath = Path.Combine(datFilesPath, datHash32 + ".lz");

            if(File.Exists(compressedDatPath))
                File.Delete(compressedDatPath);

            Dispatcher.UIThread.Post(_view.Close);
        });
    }
}