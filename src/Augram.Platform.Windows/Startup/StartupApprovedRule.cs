namespace Augram.Platform.Windows.Startup;

/// <summary>
/// How Task Manager's Startup apps tab records its switch: a 12-byte <c>REG_BINARY</c> under
/// <c>HKCU\Software\Microsoft\Windows\CurrentVersion\Explorer\StartupApproved\Run</c> named like the <c>Run</c> value. The
/// first byte is even when enabled (<c>02</c>, also <c>06</c>) and odd when disabled (<c>03</c>, also <c>07</c>), followed by
/// three zero bytes and, when disabled, the time it was switched off (a <c>FILETIME</c>). No value means enabled: Windows
/// writes one only once the switch has been used. Electron reads and writes the same value (<c>browser_win.cc</c>: it
/// writes <c>03</c> + zeros to disable, deletes the value to enable).
/// </summary>
internal static class StartupApprovedRule
{
    /// <summary>True when the value says Task Manager switched the entry off; absent or empty is enabled.</summary>
    public static bool IsDisabled(byte[]? value) => value is { Length: > 0 } && (value[0] & 1) == 1;
}
