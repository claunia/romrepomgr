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
// Copyright © 2020-2021 Natalia Portillo
*******************************************************************************/

using System.IO;
using System.Reactive;
using System.Threading.Tasks;
using Avalonia.Threading;
using JetBrains.Annotations;
using ReactiveUI;
using RomRepoMgr.Core;
using RomRepoMgr.Core.EventArgs;
using RomRepoMgr.Core.Workers;
using RomRepoMgr.Resources;
using RomRepoMgr.Views;
using ErrorEventArgs = RomRepoMgr.Core.EventArgs.ErrorEventArgs;

namespace RomRepoMgr.ViewModels
{
    public sealed class ExportDatViewModel : ViewModelBase
    {
        readonly string      _datHash;
        readonly string      _outPath;
        readonly ExportDat   _view;
        readonly Compression _worker;
        bool                 _canClose;
        string               _errorMessage;
        bool                 _errorVisible;
        bool                 _progressVisible;
        string               _statusMessage;

        public ExportDatViewModel(ExportDat view, string datHash, string outPath)
        {
            _view                    =  view;
            _datHash                 =  datHash;
            _outPath                 =  outPath;
            CloseCommand             =  ReactiveCommand.Create(ExecuteCloseCommand);
            ProgressVisible          =  false;
            ErrorVisible             =  false;
            _worker                  =  new Compression();
            _worker.FinishedWithText += OnWorkerOnFinishedWithText;
            _worker.FailedWithText   += OnWorkerOnFailedWithText;
        }

        [NotNull]
        public string Title => Localization.ExportDatTitle;

        public string StatusMessage
        {
            get => _statusMessage;
            set => this.RaiseAndSetIfChanged(ref _statusMessage, value);
        }

        public bool ProgressVisible
        {
            get => _progressVisible;
            set => this.RaiseAndSetIfChanged(ref _progressVisible, value);
        }

        public bool ErrorVisible
        {
            get => _errorVisible;
            set => this.RaiseAndSetIfChanged(ref _errorVisible, value);
        }

        public string ErrorMessage
        {
            get => _errorMessage;
            set => this.RaiseAndSetIfChanged(ref _errorMessage, value);
        }

        public bool CanClose
        {
            get => _canClose;
            set => this.RaiseAndSetIfChanged(ref _canClose, value);
        }

        public string                      CloseLabel   => Localization.CloseLabel;
        public ReactiveCommand<Unit, Unit> CloseCommand { get; }

        void OnWorkerOnFinishedWithText(object sender, MessageEventArgs args) => Dispatcher.UIThread.Post(() =>
        {
            StatusMessage   = Localization.Finished;
            ProgressVisible = false;
            CanClose        = true;
        });

        void OnWorkerOnFailedWithText(object sender, ErrorEventArgs args) => Dispatcher.UIThread.Post(() =>
        {
            ErrorMessage    = args.Message;
            ProgressVisible = false;
            ErrorVisible    = true;
            CanClose        = true;
        });

        void ExecuteCloseCommand() => _view.Close();

        internal void OnOpened()
        {
            ProgressVisible = true;
            StatusMessage   = Localization.DecompressingDat;

            byte[] sha384Bytes = new byte[48];
            string sha384      = _datHash;

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

            if(!File.Exists(compressedDatPath))
                _view.Close();

            Task.Run(() => _worker.DecompressFile(compressedDatPath, _outPath));
        }
    }
}