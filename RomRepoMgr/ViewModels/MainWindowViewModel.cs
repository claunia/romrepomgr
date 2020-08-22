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

using System.Reactive;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using ReactiveUI;
using RomRepoMgr.Views;

namespace RomRepoMgr.ViewModels
{
    public class MainWindowViewModel : ViewModelBase
    {
        readonly MainWindow _view;

        public MainWindowViewModel(MainWindow view)
        {
            _view           = view;
            ExitCommand     = ReactiveCommand.Create(ExecuteExitCommand);
            SettingsCommand = ReactiveCommand.Create(ExecuteSettingsCommand);
            AboutCommand    = ReactiveCommand.Create(ExecuteAboutCommand);
        }

        public string Greeting => "Hello World!";
        public bool NativeMenuSupported =>
            NativeMenu.GetIsNativeMenuExported((Application.Current.ApplicationLifetime as
                                                    IClassicDesktopStyleApplicationLifetime)?.MainWindow);

        public ReactiveCommand<Unit, Unit> AboutCommand    { get; }
        public ReactiveCommand<Unit, Unit> ExitCommand     { get; }
        public ReactiveCommand<Unit, Unit> SettingsCommand { get; }

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
    }
}