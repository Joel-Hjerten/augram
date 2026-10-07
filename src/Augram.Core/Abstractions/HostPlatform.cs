namespace Augram.Core.Abstractions;

/// <summary>
/// The platforms Augram runs on, as a step remembers where it was authored (F8) and a platform adapter
/// names itself. Core never branches on this for behaviour; it stores it and shows it.
/// </summary>
public enum HostPlatform
{
    Windows,
    MacOS,
}
