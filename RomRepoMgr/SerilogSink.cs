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

#nullable enable
using System.Collections.Generic;
using Avalonia.Logging;

namespace RomRepoMgr;

public class SerilogSink(LogEventLevel minimumLevel, IList<string>? areas = null) : ILogSink
{
    private readonly IList<string>? _areas = areas?.Count > 0 ? areas : null;

    public bool IsEnabled(LogEventLevel level, string area) =>
        level >= minimumLevel && (_areas?.Contains(area) ?? true);

    public void Log(LogEventLevel level, string area, object? source, string messageTemplate)
    {
        if(IsEnabled(level, area))
        {
            Serilog.Log.Write(LogLevelToSerilogLevel(level),
                              "[{Area} {Source}] {MessageTemplate}",
                              area,
                              source,
                              messageTemplate);
        }
    }

    public void Log(LogEventLevel    level, string area, object? source, string messageTemplate,
                    params object?[] propertyValues)
    {
        if(IsEnabled(level, area))
        {
            Serilog.Log.Write(LogLevelToSerilogLevel(level),
                              "[{Area} {Source}] {MessageTemplate}",
                              propertyValues,
                              area,
                              source,
                              messageTemplate);
        }
    }

    private static Serilog.Events.LogEventLevel LogLevelToSerilogLevel(LogEventLevel level)
    {
        return level switch
               {
                   LogEventLevel.Verbose     => Serilog.Events.LogEventLevel.Verbose,
                   LogEventLevel.Debug       => Serilog.Events.LogEventLevel.Debug,
                   LogEventLevel.Information => Serilog.Events.LogEventLevel.Information,
                   LogEventLevel.Warning     => Serilog.Events.LogEventLevel.Warning,
                   LogEventLevel.Error       => Serilog.Events.LogEventLevel.Error,
                   LogEventLevel.Fatal       => Serilog.Events.LogEventLevel.Fatal,
                   _                         => Serilog.Events.LogEventLevel.Verbose
               };
    }
}