namespace Augram.Core.Abstractions;

/// <summary>
/// The platforms Augram runs on, as a step remembers where it was authored (F8) and a platform adapter
/// names itself. Core stores it, shows it and picks a platform's own part by it (an app group's names, a command's steps);
/// it never asks the operating system: the platform this process runs as is <see cref="RunningPlatform.Current"/>. The
/// name a person reads and the other platform are <see cref="HostPlatformExtensions"/>.
/// </summary>
public enum HostPlatform
{
    Windows,
    MacOS,
}
