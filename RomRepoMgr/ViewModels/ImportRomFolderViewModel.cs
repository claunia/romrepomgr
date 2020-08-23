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
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Reactive;
using System.Threading.Tasks;
using Avalonia.Threading;
using JetBrains.Annotations;
using ReactiveUI;
using RomRepoMgr.Core.EventArgs;
using RomRepoMgr.Core.Workers;
using RomRepoMgr.Views;

namespace RomRepoMgr.ViewModels
{
    public sealed class ImportRomFolderViewModel : ViewModelBase
    {
        readonly ImportRomFolder _view;
        bool                     _canClose;
        bool                     _canStart;
        bool                     _isImporting;
        bool                     _isReady;
        bool                     _knownOnlyChecked;
        bool                     _progress2IsIndeterminate;
        double                   _progress2Maximum;
        double                   _progress2Minimum;
        double                   _progress2Value;
        bool                     _progress2Visible;
        bool                     _progressIsIndeterminate;
        double                   _progressMaximum;
        double                   _progressMinimum;
        double                   _progressValue;
        bool                     _progressVisible;
        bool                     _recurseArchivesChecked;
        bool                     _removeFilesChecked;
        bool                     _removeFilesEnabled;
        string                   _status2Message;
        string                   _statusMessage;

        public ImportRomFolderViewModel(ImportRomFolder view, string folderPath)
        {
            _view                   = view;
            FolderPath              = folderPath;
            _removeFilesChecked     = false;
            _knownOnlyChecked       = true;
            _recurseArchivesChecked = Settings.Settings.UnArUsable;
            ImportResults           = new ObservableCollection<ImportRomFolderItem>();
            CloseCommand            = ReactiveCommand.Create(ExecuteCloseCommand);
            StartCommand            = ReactiveCommand.Create(ExecuteStartCommand);
            IsReady                 = true;
            CanStart                = true;
            CanClose                = true;
            _removeFilesEnabled     = false;
        }

        public string PathLabel              => "Path:";
        public string FolderPath             { get; }
        public string RemoveFilesLabel       => "Remove files after import successful.";
        public string KnownOnlyLabel         => "Only import known files.";
        public string RecurseArchivesLabel   => "Try to detect archives and import their contents.";
        public bool   RecurseArchivesEnabled => Settings.Settings.UnArUsable;

        public bool RemoveFilesChecked
        {
            get => _removeFilesChecked;
            set => this.RaiseAndSetIfChanged(ref _removeFilesChecked, value);
        }

        public bool KnownOnlyChecked
        {
            get => _knownOnlyChecked;
            set => this.RaiseAndSetIfChanged(ref _knownOnlyChecked, value);
        }

        public bool RemoveFilesEnabled
        {
            get => _removeFilesEnabled;
            set => this.RaiseAndSetIfChanged(ref _removeFilesEnabled, value);
        }

        public bool RecurseArchivesChecked
        {
            get => _recurseArchivesChecked;
            set
            {
                if(value)
                    RemoveFilesChecked = false;

                RemoveFilesEnabled = !value;
                this.RaiseAndSetIfChanged(ref _recurseArchivesChecked, value);
            }
        }

        public bool IsReady
        {
            get => _isReady;
            set => this.RaiseAndSetIfChanged(ref _isReady, value);
        }

        public bool ProgressVisible
        {
            get => _progressVisible;
            set => this.RaiseAndSetIfChanged(ref _progressVisible, value);
        }

        public string StatusMessage
        {
            get => _statusMessage;
            set => this.RaiseAndSetIfChanged(ref _statusMessage, value);
        }

        public double ProgressMinimum
        {
            get => _progressMinimum;
            set => this.RaiseAndSetIfChanged(ref _progressMinimum, value);
        }

        public double ProgressMaximum
        {
            get => _progressMaximum;
            set => this.RaiseAndSetIfChanged(ref _progressMaximum, value);
        }

        public double ProgressValue
        {
            get => _progressValue;
            set => this.RaiseAndSetIfChanged(ref _progressValue, value);
        }

        public bool ProgressIsIndeterminate
        {
            get => _progressIsIndeterminate;
            set => this.RaiseAndSetIfChanged(ref _progressIsIndeterminate, value);
        }

        public bool Progress2Visible
        {
            get => _progress2Visible;
            set => this.RaiseAndSetIfChanged(ref _progress2Visible, value);
        }

        public string Status2Message
        {
            get => _status2Message;
            set => this.RaiseAndSetIfChanged(ref _status2Message, value);
        }

        public double Progress2Minimum
        {
            get => _progress2Minimum;
            set => this.RaiseAndSetIfChanged(ref _progress2Minimum, value);
        }

        public double Progress2Maximum
        {
            get => _progress2Maximum;
            set => this.RaiseAndSetIfChanged(ref _progress2Maximum, value);
        }

        public double Progress2Value
        {
            get => _progress2Value;
            set => this.RaiseAndSetIfChanged(ref _progress2Value, value);
        }

        public bool Progress2IsIndeterminate
        {
            get => _progress2IsIndeterminate;
            set => this.RaiseAndSetIfChanged(ref _progress2IsIndeterminate, value);
        }

        public bool IsImporting
        {
            get => _isImporting;
            set => this.RaiseAndSetIfChanged(ref _isImporting, value);
        }

        [NotNull]
        public string Title => "Import ROM files from folder...";

        public ObservableCollection<ImportRomFolderItem> ImportResults       { get; }
        public string                                    ResultFilenameLabel => "Filename";
        public string                                    ResultStatusLabel   => "Status";
        public string                                    CloseLabel          => "Close";
        public string                                    StartLabel          => "Start";

        public bool CanClose
        {
            get => _canClose;
            set => this.RaiseAndSetIfChanged(ref _canClose, value);
        }

        public bool CanStart
        {
            get => _canStart;
            set => this.RaiseAndSetIfChanged(ref _canStart, value);
        }

        public ReactiveCommand<Unit, Unit> CloseCommand { get; }
        public ReactiveCommand<Unit, Unit> StartCommand { get; }

        void ExecuteCloseCommand() => _view.Close();

        void ExecuteStartCommand()
        {
            IsReady                 = false;
            ProgressVisible         = true;
            ProgressIsIndeterminate = true;
            StatusMessage           = "Enumerating files...";
            IsImporting             = true;
            CanStart                = false;
            CanClose                = false;

            Task.Run(() =>
            {
                var      watch = new Stopwatch();
                string[] files = Directory.GetFiles(FolderPath, "*", SearchOption.AllDirectories);

                Dispatcher.UIThread.Post(() =>
                {
                    ProgressIsIndeterminate = false;
                    ProgressMinimum         = 0;
                    ProgressMaximum         = files.LongLength;
                    ProgressValue           = 0;
                    Progress2Visible        = true;
                });

                var worker = new FileImporter(KnownOnlyChecked, RemoveFilesChecked);
                worker.SetIndeterminateProgress += OnWorkerOnSetIndeterminateProgress;
                worker.SetMessage               += OnWorkerOnSetMessage;
                worker.SetProgress              += OnWorkerOnSetProgress;
                worker.SetProgressBounds        += OnWorkerOnSetProgressBounds;

                long position = 0;
                watch.Start();

                foreach(string file in files)
                {
                    Dispatcher.UIThread.Post(() =>
                    {
                        StatusMessage = string.Format("Importing {0}...", Path.GetFileName(file));
                        ProgressValue = position;
                    });

                    bool ret = worker.ImportRom(file);

                    Dispatcher.UIThread.Post(() =>
                    {
                        if(ret)
                        {
                            ImportResults.Add(new ImportRomFolderItem
                            {
                                Filename = Path.GetFileName(file),
                                Status   = "OK"
                            });
                        }
                        else
                        {
                            ImportResults.Add(new ImportRomFolderItem
                            {
                                Filename = Path.GetFileName(file),
                                Status   = string.Format("Error: {0}", worker.LastMessage)
                            });
                        }
                    });

                    position++;
                }

                worker.SaveChanges();

                watch.Stop();

                Dispatcher.UIThread.Post(() =>
                {
                    ProgressVisible  = false;
                    StatusMessage    = "Finished!";
                    CanClose         = true;
                    Progress2Visible = false;

                    Console.WriteLine($"Took {watch.Elapsed.TotalSeconds} seconds");
                });
            });
        }

        void OnWorkerOnSetProgressBounds(object sender, ProgressBoundsEventArgs args) => Dispatcher.UIThread.Post(() =>
        {
            Progress2IsIndeterminate = false;
            Progress2Maximum         = args.Maximum;
            Progress2Minimum         = args.Minimum;
        });

        void OnWorkerOnSetProgress(object sender, ProgressEventArgs args) =>
            Dispatcher.UIThread.Post(() => Progress2Value = args.Value);

        void OnWorkerOnSetMessage(object sender, MessageEventArgs args) =>
            Dispatcher.UIThread.Post(() => Status2Message = args.Message);

        void OnWorkerOnSetIndeterminateProgress(object sender, EventArgs args) =>
            Dispatcher.UIThread.Post(() => Progress2IsIndeterminate = true);
    }

    public sealed class ImportRomFolderItem
    {
        public string Filename { get; set; }
        public string Status   { get; set; }
    }
}