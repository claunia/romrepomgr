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
// Copyright © 2020-2024 Natalia Portillo
*******************************************************************************/

using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Reactive;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Avalonia.Threading;
using ReactiveUI;
using RomRepoMgr.Core.Models;
using RomRepoMgr.Resources;
using RomRepoMgr.Views;

namespace RomRepoMgr.ViewModels;

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

        Assemblies = [];

        // TODO: They do not load in time
        Task.Run(() =>
        {
            foreach(Assembly assembly in AppDomain.CurrentDomain.GetAssemblies().OrderBy(a => a.FullName))
            {
                string name = assembly.GetName().Name;

                string version =
                    (Attribute.GetCustomAttribute(assembly, typeof(AssemblyInformationalVersionAttribute)) as
                         AssemblyInformationalVersionAttribute)?.InformationalVersion;

                if(name is null || version is null) continue;

                Dispatcher.UIThread.Post(() =>
                {
                    Assemblies.Add(new AssemblyModel
                    {
                        Name    = name,
                        Version = version
                    });
                });
            }
        });
    }

    public string                              AboutLabel            => Localization.AboutLabel;
    public string                              LibrariesLabel        => Localization.LibrariesLabel;
    public string                              AuthorsLabel          => Localization.AuthorsLabel;
    public string                              Title                 => Localization.AboutTitle;
    public string                              SoftwareName          => "RomRepoMgr";
    public string                              SuiteName             => "ROM Repository Manager";
    public string                              Copyright             => "© 2020-2024 Natalia Portillo";
    public string                              Website               => "https://www.claunia.com";
    public string                              License               => Localization.LicenseLabel;
    public string                              CloseLabel            => Localization.CloseLabel;
    public string                              AssembliesLibraryText => Localization.AssembliesLibraryText;
    public string                              AssembliesVersionText => Localization.AssembliesVersionText;
    public string                              Authors               => Localization.AuthorsText;
    public ReactiveCommand<Unit, Unit>         WebsiteCommand        { get; }
    public ReactiveCommand<Unit, Unit>         LicenseCommand        { get; }
    public ReactiveCommand<Unit, Unit>         CloseCommand          { get; }
    public ObservableCollection<AssemblyModel> Assemblies            { get; }

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

        if(RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            process.StartInfo.FileName  = "cmd";
            process.StartInfo.Arguments = $"/c start {process.StartInfo.Arguments.Replace("&", "^&")}";
        }
        else if(RuntimeInformation.IsOSPlatform(OSPlatform.Linux) ||
                RuntimeInformation.IsOSPlatform(OSPlatform.FreeBSD))
            process.StartInfo.FileName = "xdg-open";
        else if(RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            process.StartInfo.FileName = "open";
        else
        {
            if(Debugger.IsAttached) throw new ArgumentOutOfRangeException();

            return;
        }

        process.Start();
    }

    void ExecuteLicenseCommand()
    {
        /*            var dialog = new LicenseDialog();
                    dialog.DataContext = new LicenseViewModel(dialog);
                    dialog.ShowWindowDialogAsync(_view);*/
    }

    void ExecuteCloseCommand() => _view.Close();
}