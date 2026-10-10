using System.Runtime.Versioning;
using Microsoft.Win32;

namespace Augram.Platform.Windows.Startup;

/// <summary>
/// The real <see cref="IWin32StartupRegistry"/> over <c>HKEY_CURRENT_USER</c> (per user, no elevation). The key paths are
/// parameters so its own test can run against a throwaway key instead of the real <c>Run</c> key. Registry failures surface
/// as <see cref="IOException"/>, <see cref="UnauthorizedAccessException"/> or <see cref="System.Security.SecurityException"/>.
/// </summary>
[SupportedOSPlatform("windows")]
internal sealed class Win32StartupRegistry(string runKeyPath, string approvedKeyPath) : IWin32StartupRegistry
{
    public Win32StartupRegistry()
        : this(RunKeyStartupRegistration.RunKeyPath, RunKeyStartupRegistration.StartupApprovedKeyPath)
    {
    }

    public string? ReadCommand(string name)
    {
        using var key = Registry.CurrentUser.OpenSubKey(runKeyPath, writable: false);
        return key?.GetValue(name) as string;
    }

    public void WriteCommand(string name, string command)
    {
        using var key = Registry.CurrentUser.CreateSubKey(runKeyPath, writable: true);
        key.SetValue(name, command, RegistryValueKind.String);
    }

    public void DeleteCommand(string name) => Delete(runKeyPath, name);

    public byte[]? ReadApproval(string name)
    {
        using var key = Registry.CurrentUser.OpenSubKey(approvedKeyPath, writable: false);
        return key?.GetValue(name) as byte[];
    }

    public void DeleteApproval(string name) => Delete(approvedKeyPath, name);

    private static void Delete(string path, string name)
    {
        using var key = Registry.CurrentUser.OpenSubKey(path, writable: true);
        key?.DeleteValue(name, throwOnMissingValue: false);
    }
}
