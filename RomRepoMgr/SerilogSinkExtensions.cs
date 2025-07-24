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