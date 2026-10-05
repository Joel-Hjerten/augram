namespace Augram.Core.Abstractions;

/// <summary>
/// "Start at login" as the OS keeps it (F7): the <c>Run</c> registry value on Windows, a login item
/// on macOS. The setting of the same name in <c>Config.GeneralSettings</c> is what the user chose;
/// the App keeps the two in step. Both members may touch the OS and may throw <see cref="IOException"/>
/// or <see cref="UnauthorizedAccessException"/>; the caller reports, never the port.
/// </summary>
public interface IStartupRegistration
{
    bool IsEnabled { get; }

    void Set(bool enabled);
}
