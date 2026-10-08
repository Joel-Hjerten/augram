namespace Augram.App.Hosting;

/// <summary>The build channel (MSBuild <c>AugramChannel</c>, read by <see cref="AppInfo"/>): Dev for local and CI builds, Release for the installed Augram.</summary>
public enum AppChannel
{
    Dev,
    Release,
}
