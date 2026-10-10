namespace Augram.Platform.Windows.Startup;

/// <summary>
/// The two per-user registry values start at login reads and writes, as thin as the registry (the real one is
/// <see cref="Win32StartupRegistry"/>): the command under <c>Run</c>, and Task Manager's on/off record of the same name
/// under <c>Explorer\StartupApproved\Run</c>. A seam so <see cref="RunKeyStartupRegistration"/>'s rules are tested against
/// scripted values; no test writes the real Run key.
/// </summary>
internal interface IWin32StartupRegistry
{
    /// <summary>The <c>Run</c> value's command line; null when absent or not a string.</summary>
    string? ReadCommand(string name);

    void WriteCommand(string name, string command);

    /// <summary>Removes the <c>Run</c> value; absent is fine.</summary>
    void DeleteCommand(string name);

    /// <summary>The <c>StartupApproved\Run</c> value's bytes (<see cref="StartupApprovedRule"/>); null when absent or not binary.</summary>
    byte[]? ReadApproval(string name);

    /// <summary>Removes the <c>StartupApproved\Run</c> value, which Windows reads as enabled; absent is fine.</summary>
    void DeleteApproval(string name);
}
