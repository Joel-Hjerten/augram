using Augram.Core.Abstractions;

namespace Augram.Core.Diagnostics;

/// <summary>
/// Call-site sugar for <see cref="IEventLog"/>. Each method checks <see cref="IEventLog.IsEnabled"/>
/// first and only then builds the <see cref="LogEvent"/>, so a disabled level costs one virtual call
/// and no allocation. Properties arrive as a <c>params ReadOnlySpan</c>, which the compiler places on the
/// stack, each written as a tuple: <c>log.Info("hook", "Hook installed", ("generation", 2))</c>. A value-type
/// property value is boxed at the call site regardless; on the hot path guard such calls with <c>IsEnabled</c> yourself.
/// </summary>
public static class EventLogExtensions
{
    public static void Trace(this IEventLog log, string source, string message, params ReadOnlySpan<LogProperty> properties)
        => Emit(log, EventLevel.Trace, source, message, properties, null);

    public static void Debug(this IEventLog log, string source, string message, params ReadOnlySpan<LogProperty> properties)
        => Emit(log, EventLevel.Debug, source, message, properties, null);

    public static void Info(this IEventLog log, string source, string message, params ReadOnlySpan<LogProperty> properties)
        => Emit(log, EventLevel.Info, source, message, properties, null);

    public static void Warning(this IEventLog log, string source, string message, params ReadOnlySpan<LogProperty> properties)
        => Emit(log, EventLevel.Warning, source, message, properties, null);

    public static void Error(this IEventLog log, string source, string message, Exception? exception, params ReadOnlySpan<LogProperty> properties)
        => Emit(log, EventLevel.Error, source, message, properties, exception);

    public static void Error(this IEventLog log, string source, string message, params ReadOnlySpan<LogProperty> properties)
        => Emit(log, EventLevel.Error, source, message, properties, null);

    private static void Emit(
        IEventLog log,
        EventLevel level,
        string source,
        string message,
        ReadOnlySpan<LogProperty> properties,
        Exception? exception)
    {
        if (!log.IsEnabled(level))
        {
            return;
        }

        log.Log(new LogEvent(DateTimeOffset.Now, level, source, message, properties.Length == 0 ? null : properties.ToArray(), exception));
    }
}
