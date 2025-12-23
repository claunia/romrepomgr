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
using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using RomRepoMgr.Core.EventArgs;

namespace RomRepoMgr.Models;

public partial class RomImporter : ObservableObject
{
    [ObservableProperty]
    bool _indeterminate;
    [ObservableProperty]
    double _maximum;
    [ObservableProperty]
    double _minimum;
    [ObservableProperty]
    double _progress;
    [ObservableProperty]
    bool _progressVisible = true;
    [ObservableProperty]
    Color _statusColor;
    [ObservableProperty]
    string _statusMessage;
    public string Filename { get; internal init; }
    public bool   Running  { get; private set; } = true;

    internal void OnSetIndeterminateProgress(object sender, EventArgs e)
    {
        Indeterminate = true;
    }

    internal void OnSetMessage(object sender, MessageEventArgs e)
    {
        StatusMessage = e.Message;
    }

    internal void OnSetProgress(object sender, ProgressEventArgs e)
    {
        Progress = e.Value;
    }

    internal void OnSetProgressBounds(object sender, ProgressBoundsEventArgs e)
    {
        Indeterminate = false;
        Maximum       = e.Maximum;
        Minimum       = e.Minimum;
    }

    internal void OnWorkFinished(object sender, MessageEventArgs e)
    {
        Indeterminate   = false;
        Maximum         = 1;
        Minimum         = 0;
        Progress        = 1;
        StatusMessage   = e.Message;
        Running         = false;
        ProgressVisible = false;
    }

    public void OnImportedRom(object sender, ImportedRomItemEventArgs e)
    {
        Indeterminate   = false;
        Maximum         = 1;
        Minimum         = 0;
        Progress        = 1;
        StatusMessage   = e.Item.Status;
        Running         = false;
        ProgressVisible = false;
    }
}