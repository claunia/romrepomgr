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
using System.Reactive;
using System.Threading.Tasks;
using Avalonia.Threading;
using JetBrains.Annotations;
using ReactiveUI;
using RomRepoMgr.Core.EventArgs;
using RomRepoMgr.Core.Workers;
using RomRepoMgr.Resources;
using RomRepoMgr.Views;

namespace RomRepoMgr.ViewModels
{
    public sealed class ImportDatViewModel : ViewModelBase
    {
        readonly ImportDat   _view;
        readonly DatImporter _worker;
        bool                 _canClose;
        double               _currentValue;
        string               _errorMessage;
        bool                 _errorVisible;
        bool                 _indeterminateProgress;
        double               _maximumValue;
        double               _minimumValue;
        bool                 _progressVisible;
        string               _statusMessage;

        public ImportDatViewModel(ImportDat view, string datPath)
        {
            _view                            =  view;
            CloseCommand                     =  ReactiveCommand.Create(ExecuteCloseCommand);
            IndeterminateProgress            =  true;
            ProgressVisible                  =  false;
            ErrorVisible                     =  false;
            _worker                          =  new DatImporter(datPath, null);
            _worker.ErrorOccurred            += OnWorkerOnErrorOccurred;
            _worker.SetIndeterminateProgress += OnWorkerOnSetIndeterminateProgress;
            _worker.SetMessage               += OnWorkerOnSetMessage;
            _worker.SetProgress              += OnWorkerOnSetProgress;
            _worker.SetProgressBounds        += OnWorkerOnSetProgressBounds;
            _worker.WorkFinished             += OnWorkerOnWorkFinished;
        }

        [NotNull]
        public string Title => Localization.ImportDatTitle;

        public string StatusMessage
        {
            get => _statusMessage;
            set => this.RaiseAndSetIfChanged(ref _statusMessage, value);
        }

        public bool IndeterminateProgress
        {
            get => _indeterminateProgress;
            set => this.RaiseAndSetIfChanged(ref _indeterminateProgress, value);
        }

        public double MaximumValue
        {
            get => _maximumValue;
            set => this.RaiseAndSetIfChanged(ref _maximumValue, value);
        }

        public double MinimumValue
        {
            get => _minimumValue;
            set => this.RaiseAndSetIfChanged(ref _minimumValue, value);
        }

        public double CurrentValue
        {
            get => _currentValue;
            set => this.RaiseAndSetIfChanged(ref _currentValue, value);
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

        void OnWorkerOnWorkFinished(object sender, MessageEventArgs args) => Dispatcher.UIThread.Post(() =>
        {
            StatusMessage   = args.Message;
            ProgressVisible = false;
            CanClose        = true;
        });

        void OnWorkerOnSetProgressBounds(object sender, ProgressBoundsEventArgs args) => Dispatcher.UIThread.Post(() =>
        {
            IndeterminateProgress = false;
            MaximumValue          = args.Maximum;
            MinimumValue          = args.Minimum;
        });

        void OnWorkerOnSetProgress(object sender, ProgressEventArgs args) =>
            Dispatcher.UIThread.Post(() => CurrentValue = args.Value);

        void OnWorkerOnSetMessage(object sender, MessageEventArgs args) =>
            Dispatcher.UIThread.Post(() => StatusMessage = args.Message);

        void OnWorkerOnSetIndeterminateProgress(object sender, EventArgs args) =>
            Dispatcher.UIThread.Post(() => IndeterminateProgress = true);

        void OnWorkerOnErrorOccurred(object sender, ErrorEventArgs args) => Dispatcher.UIThread.Post(() =>
        {
            ErrorMessage    = args.Message;
            ProgressVisible = false;
            ErrorVisible    = true;
            CanClose        = true;
        });

        void ExecuteCloseCommand() => _view.Close();

        internal void OnOpened()
        {
            ProgressVisible     =  true;
            _worker.RomSetAdded += RomSetAdded;
            Task.Run(_worker.Import);
        }

        public event EventHandler<RomSetEventArgs> RomSetAdded;
    }
}