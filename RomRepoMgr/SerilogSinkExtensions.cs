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

using Avalonia;
using Avalonia.Logging;

namespace RomRepoMgr;

public static class SerilogSinkExtensions
{
    public static AppBuilder LogToSerilog(this   AppBuilder builder, LogEventLevel level = LogEventLevel.Warning,
                                          params string[]   areas)
    {
        Logger.Sink = new SerilogSink(level, areas);

        return builder;
    }
}