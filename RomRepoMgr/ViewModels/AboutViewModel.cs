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
// Copyright © 2020-2024 Natalia Portillo
*******************************************************************************/

using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Reactive;
using System.Reflection;
using System.Threading.Tasks;
using JetBrains.Annotations;
using Microsoft.DotNet.PlatformAbstractions;
using ReactiveUI;
using RomRepoMgr.Core.Models;
using RomRepoMgr.Resources;
using RomRepoMgr.Views;

namespace RomRepoMgr.ViewModels
{
    public sealed class AboutViewModel : ViewModelBase
    {
        readonly About _view;
        string         _versionText;

        public AboutViewModel(About view)
        {
            _view = view;

            VersionText =
                (Attribute.GetCustomAttribute(typeof(App).Assembly, typeof(AssemblyInformationalVersionAttribute)) as
                     AssemblyInformationalVersionAttribute)?.InformationalVersion;

            WebsiteCommand = ReactiveCommand.Create(ExecuteWebsiteCommand);
            LicenseCommand = ReactiveCommand.Create(ExecuteLicenseCommand);
            CloseCommand   = ReactiveCommand.Create(ExecuteCloseCommand);

            Assemblies = new ObservableCollection<AssemblyModel>();

            // TODO: They do not load in time
            Task.Run(() =>
            {
                foreach(Assembly assembly in AppDomain.CurrentDomain.GetAssemblies().OrderBy(a => a.FullName))
                {
                    string name = assembly.GetName().Name;

                    string version =
                        (Attribute.GetCustomAttribute(assembly, typeof(AssemblyInformationalVersionAttribute)) as
                             AssemblyInformationalVersionAttribute)?.InformationalVersion;

                    if(name is null ||
                       version is null)
                        continue;

                    Assemblies.Add(new AssemblyModel
                    {
                        Name    = name,
                        Version = version
                    });
                }
            });
        }

        [NotNull]
        public string AboutLabel => Localization.AboutLabel;
        [NotNull]
        public string LibrariesLabel => Localization.LibrariesLabel;
        [NotNull]
        public string AuthorsLabel => Localization.AuthorsLabel;
        [NotNull]
        public string Title => Localization.AboutTitle;
        [NotNull]
        public string SoftwareName => "RomRepoMgr";
        [NotNull]
        public string SuiteName => "ROM Repository Manager";
        [NotNull]
        public string Copyright => "© 2020-2024 Natalia Portillo";
        [NotNull]
        public string Website => "https://www.claunia.com";
        [NotNull]
        public string License => Localization.LicenseLabel;
        [NotNull]
        public string CloseLabel => Localization.CloseLabel;
        [NotNull]
        public string AssembliesLibraryText => Localization.AssembliesLibraryText;
        [NotNull]
        public string AssembliesVersionText => Localization.AssembliesVersionText;
        [NotNull]
        public string Authors => Localization.AuthorsText;
        public ReactiveCommand<Unit, Unit>         WebsiteCommand { get; }
        public ReactiveCommand<Unit, Unit>         LicenseCommand { get; }
        public ReactiveCommand<Unit, Unit>         CloseCommand   { get; }
        public ObservableCollection<AssemblyModel> Assemblies     { get; }

        public string VersionText
        {
            get => _versionText;
            set => this.RaiseAndSetIfChanged(ref _versionText, value);
        }

        void ExecuteWebsiteCommand()
        {
            var process = new Process
            {
                StartInfo =
                {
                    UseShellExecute = false,
                    CreateNoWindow  = true,
                    Arguments       = "https://www.claunia.com"
                }
            };

            switch(RuntimeEnvironment.OperatingSystemPlatform)
            {
                case Platform.Unknown: return;
                case Platform.Windows:
                    process.StartInfo.FileName  = "cmd";
                    process.StartInfo.Arguments = $"/c start {process.StartInfo.Arguments.Replace("&", "^&")}";

                    break;
                case Platform.FreeBSD:
                case Platform.Linux:
                    process.StartInfo.FileName = "xdg-open";

                    break;
                case Platform.Darwin:
                    process.StartInfo.FileName = "open";

                    break;
                default:
                    if(Debugger.IsAttached)
                        throw new ArgumentOutOfRangeException();

                    return;
            }

            process.Start();
        }

        void ExecuteLicenseCommand()
        {
            /*            var dialog = new LicenseDialog();
                        dialog.DataContext = new LicenseViewModel(dialog);
                        dialog.ShowDialog(_view);*/
        }

        void ExecuteCloseCommand() => _view.Close();
    }
}