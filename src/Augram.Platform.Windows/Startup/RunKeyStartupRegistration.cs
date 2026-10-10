using System.Runtime.Versioning;
using Augram.Core.Abstractions;

namespace Augram.Platform.Windows.Startup;

/// <summary>
/// Start at login (F7) through <c>HKCU\Software\Microsoft\Windows\CurrentVersion\Run</c>: value <see cref="ValueName"/> =
/// <see cref="Command"/>, the quoted executable path followed by the App's arguments (<c>--hidden</c>, so a login launch
/// starts in the tray). Per-user, no elevation. <see cref="Status"/>: no value is not registered; Task Manager's switch
/// off (<see cref="StartupApprovedRule"/>) is disabled by the user; a value with another command (another path, or the
/// bare quoted path that the 0.5–0.7 builds wrote) is outdated; otherwise registered. <see cref="Set"/> with true writes the
/// command and deletes Task Manager's record, so turning it on in Augram also undoes a switch-off there (on means on, as
/// Electron's <c>enabled: true</c>); false deletes both. The registry calls may throw <see cref="IOException"/>,
/// <see cref="UnauthorizedAccessException"/> or <see cref="System.Security.SecurityException"/>; the App reports those.
/// </summary>
[SupportedOSPlatform("windows")]
public sealed class RunKeyStartupRegistration : IStartupRegistration
{
    public const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    public const string StartupApprovedKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Explorer\StartupApproved\Run";
    public const string DefaultValueName = "Augram";

    private readonly IWin32StartupRegistry _registry;

    /// <param name="arguments">Appended to the quoted path of this process's executable (the App passes <c>--hidden</c>); null for none.</param>
    public RunKeyStartupRegistration(string? arguments = null)
        : this(new Win32StartupRegistry(), DefaultValueName, CommandFor(Environment.ProcessPath, arguments))
    {
    }

    /// <param name="registry">The registry; tests pass a scripted one.</param>
    /// <param name="valueName">The value under both keys.</param>
    /// <param name="command">The command line to register.</param>
    internal RunKeyStartupRegistration(IWin32StartupRegistry registry, string valueName, string command)
    {
        ArgumentNullException.ThrowIfNull(registry);
        ArgumentException.ThrowIfNullOrWhiteSpace(valueName);
        ArgumentException.ThrowIfNullOrWhiteSpace(command);
        _registry = registry;
        ValueName = valueName;
        Command = command;
    }

    public string ValueName { get; }

    public string Command { get; }

    public StartupStatus Status
    {
        get
        {
            var registered = _registry.ReadCommand(ValueName);
            if (registered is null)
            {
                return StartupStatus.NotRegistered;
            }

            if (StartupApprovedRule.IsDisabled(_registry.ReadApproval(ValueName)))
            {
                return StartupStatus.DisabledByUser;
            }

            // Paths are case-insensitive on Windows; the arguments are Augram's own.
            return string.Equals(registered.Trim(), Command, StringComparison.OrdinalIgnoreCase) ? StartupStatus.Registered : StartupStatus.Outdated;
        }
    }

    /// <summary>The registered command line, or null when not registered.</summary>
    public string? RegisteredCommand => _registry.ReadCommand(ValueName);

    /// <summary><c>"C:\…\Augram.exe" --hidden</c>: the path quoted (it may hold spaces), then the arguments.</summary>
    public static string CommandFor(string? executablePath, string? arguments)
    {
        var quoted = $"\"{executablePath}\"";
        return string.IsNullOrWhiteSpace(arguments) ? quoted : $"{quoted} {arguments.Trim()}";
    }

    public void Set(bool enabled)
    {
        if (enabled)
        {
            _registry.WriteCommand(ValueName, Command);
        }
        else
        {
            _registry.DeleteCommand(ValueName);
        }

        _registry.DeleteApproval(ValueName);
    }
}
