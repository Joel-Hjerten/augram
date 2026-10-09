using Avalonia.Headless.XUnit;
using Avalonia.Platform;
using Xunit;

namespace Augram.App.Tests.Hosting;

/// <summary>A development build on a Mac shows Augram's icon in the Dock, not "exec" (Joel, 2026-10-09): the icon ships as a resource.</summary>
public sealed class DockIconTests
{
    [AvaloniaFact]
    public void TheMacIconIsAResource()
    {
        Assert.True(AssetLoader.Exists(new Uri("avares://Augram.App/Icons/augram.icns")));
    }
}
