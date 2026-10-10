namespace Augram.Core.Abstractions;

/// <summary>
/// "Start at login" as the OS keeps it (F7): the <c>Run</c> registry value on Windows, a login item
/// on macOS. The setting of the same name in <c>Config.GeneralSettings</c> is what the user chose;
/// the App keeps the two in step (and follows the OS when the user turned it off there, <see cref="StartupStatus"/>).
/// Both members may touch the OS and may throw <see cref="IOException"/>, <see cref="UnauthorizedAccessException"/>,
/// <see cref="System.Security.SecurityException"/> or <see cref="StartupRegistrationException"/>; the caller reports, never the port.
/// </summary>
public interface IStartupRegistration
{
    StartupStatus Status { get; }

    /// <summary>True registers (rewriting an outdated entry and clearing an "off" made outside Augram: on means on); false removes.</summary>
    void Set(bool enabled);
}
