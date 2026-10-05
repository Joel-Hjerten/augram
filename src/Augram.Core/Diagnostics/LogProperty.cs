namespace Augram.Core.Diagnostics;

/// <summary>
/// One <c>key=value</c> on a <see cref="LogEvent"/>. Written as a tuple at the call site:
/// <c>log.Info("hook", "Hook installed", ("generation", 2))</c>. Keys are short camelCase
/// identifiers; values are strings, numbers, bools or enums that the sinks render invariantly.
/// </summary>
public readonly record struct LogProperty(string Key, object? Value)
{
    public static implicit operator LogProperty((string Key, object? Value) pair) => new(pair.Key, pair.Value);

    public static LogProperty FromTuple((string Key, object? Value) pair) => pair;
}
