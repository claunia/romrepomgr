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