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

using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Reactive;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using ReactiveUI;
using RomRepoMgr.Core.Models;
using RomRepoMgr.Views;

namespace RomRepoMgr.ViewModels
{
    public class MainWindowViewModel : ViewModelBase
    {
        readonly MainWindow _view;

        public MainWindowViewModel(MainWindow view, List<RomSetModel> romSets)
        {
            _view            = view;
            ExitCommand      = ReactiveCommand.Create(ExecuteExitCommand);
            SettingsCommand  = ReactiveCommand.Create(ExecuteSettingsCommand);
            AboutCommand     = ReactiveCommand.Create(ExecuteAboutCommand);
            ImportDatCommand = ReactiveCommand.Create(ExecuteImportDatCommand);
            RomSets          = new ObservableCollection<RomSetModel>(romSets);
        }

        public ObservableCollection<RomSetModel> RomSets                { get; }
        public string                            RomSetLabel            => "ROM sets";
        public string                            RomSetNameLabel        => "Name";
        public string                            RomSetVersionLabel     => "Version";
        public string                            RomSetAuthorLabel      => "Author";
        public string                            RomSetDateLabel        => "Date";
        public string                            RomSetDescriptionLabel => "Description";
        public string                            RomSetCommentLabel     => "Comment";
        public string                            RomSetHomepageLabel    => "Homepage";

        public string Greeting => "Hello World!";
        public bool NativeMenuSupported =>
            NativeMenu.GetIsNativeMenuExported((Application.Current.ApplicationLifetime as
                                                    IClassicDesktopStyleApplicationLifetime)?.MainWindow);

        public ReactiveCommand<Unit, Unit> AboutCommand     { get; }
        public ReactiveCommand<Unit, Unit> ExitCommand      { get; }
        public ReactiveCommand<Unit, Unit> SettingsCommand  { get; }
        public ReactiveCommand<Unit, Unit> ImportDatCommand { get; }

        internal async void ExecuteSettingsCommand()
        {
            /*var dialog = new SettingsDialog();
            dialog.DataContext = new SettingsViewModel(dialog, false);
            await dialog.ShowDialog(_view);*/
        }

        internal void ExecuteExitCommand() =>
            (Application.Current.ApplicationLifetime as ClassicDesktopStyleApplicationLifetime)?.Shutdown();

        internal void ExecuteAboutCommand()
        {
            var dialog = new About();
            dialog.DataContext = new AboutViewModel(dialog);
            dialog.ShowDialog(_view);
        }

        internal async void ExecuteImportDatCommand()
        {
            var dlgOpen = new OpenFileDialog();
            dlgOpen.AllowMultiple = false;
            dlgOpen.Title         = "Import DAT file...";

            dlgOpen.Filters.Add(new FileDialogFilter
            {
                Extensions = new List<string>
                {
                    "*.dat",
                    "*.xml"
                },
                Name = "DAT files"
            });

            dlgOpen.Filters.Add(new FileDialogFilter
            {
                Extensions = new List<string>
                {
                    "*.*"
                },
                Name = "All files"
            });

            string[] result = await dlgOpen.ShowAsync(_view);

            if(result?.Length != 1)
                return;

            var dialog             = new ImportDat();
            var importDatViewModel = new ImportDatViewModel(dialog, result[0]);
            dialog.DataContext = importDatViewModel;
            await dialog.ShowDialog(_view);
        }
    }
}