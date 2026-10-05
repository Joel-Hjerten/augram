using System.Runtime.Versioning;
using Augram.Core.Abstractions;
using Microsoft.Win32;

namespace Augram.Platform.Windows.Startup;

/// <summary>
/// Start at login (F7) through <c>HKCU\Software\Microsoft\Windows\CurrentVersion\Run</c>: value
/// <see cref="ValueName"/> = the quoted executable path. Per-user, no elevation. <see cref="IsEnabled"/>
/// is "the value exists"; <see cref="Set"/> with true always rewrites the command, so toggling after the
/// executable moved repairs the entry. The registry calls may throw <see cref="IOException"/> or
/// <see cref="UnauthorizedAccessException"/>; the App reports those.
/// </summary>
[SupportedOSPlatform("windows")]
public sealed class RunKeyStartupRegistration : IStartupRegistration
{
    public const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    public const string DefaultValueName = "Augram";

    /// <param name="valueName">The Run value; tests use a throwaway name.</param>
    /// <param name="command">The command line to register; defaults to the quoted path of this process's executable.</param>
    public RunKeyStartupRegistration(string? valueName = null, string? command = null)
    {
        ValueName = valueName ?? DefaultValueName;
        Command = command ?? $"\"{Environment.ProcessPath}\"";
    }

    public string ValueName { get; }

    public string Command { get; }

    public bool IsEnabled => RegisteredCommand is not null;

    /// <summary>The registered command line, or null when not registered.</summary>
    public string? RegisteredCommand
    {
        get
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: false);
            return key?.GetValue(ValueName) as string;
        }
    }

    public void Set(bool enabled)
    {
        using var key = Registry.CurrentUser.CreateSubKey(RunKeyPath, writable: true);
        if (enabled)
        {
            key.SetValue(ValueName, Command, RegistryValueKind.String);
        }
        else
        {
            key.DeleteValue(ValueName, throwOnMissingValue: false);
        }
    }
}
