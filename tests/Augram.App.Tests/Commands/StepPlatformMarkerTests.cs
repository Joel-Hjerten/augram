using Augram.App.Components.StepList;
using Augram.Core.Abstractions;
using Augram.Core.Mapping;
using Augram.Core.Steps.Delay;
using Augram.Core.Steps.Hotkey;
using Xunit;

namespace Augram.App.Tests.Commands;

/// <summary>F8 on screen (Joel, 2026-10-07): where a step comes from and what runs here.</summary>
public sealed class StepPlatformMarkerTests
{
    private static readonly CommandStep CtrlW = new(new HotkeyStep(KeyModifiers.Control, KeyCode.W), HostPlatform.Windows);
    private static readonly CommandStep WinD = new(new HotkeyStep(KeyModifiers.Meta, KeyCode.D), HostPlatform.Windows);
    private static readonly CommandStep F5 = new(new HotkeyStep(KeyModifiers.None, KeyCode.F5), HostPlatform.Windows);
    private static readonly CommandStep Wait = new(new DelayStep(30), HostPlatform.Windows);

    [Fact]
    public void WhereItWasAuthored_AStepReadsAsItself()
    {
        Assert.Equal(new StepRowText("Ctrl+W", null), StepPlatformMarker.For(CtrlW, HostPlatform.Windows));
        Assert.Equal(new StepRowText("Win+D", null), StepPlatformMarker.For(WinD, HostPlatform.Windows));
    }

    [Fact]
    public void OnTheOtherPlatform_AStepReadsAsItsConversionOrWhyItHasNone()
    {
        Assert.Equal(new StepRowText("Ctrl+W → Cmd+W", "from Windows · converted"), StepPlatformMarker.For(CtrlW, HostPlatform.MacOS));
        Assert.Equal(new StepRowText("Win+D", "from Windows · needs a macOS version"), StepPlatformMarker.For(WinD, HostPlatform.MacOS));
        Assert.Equal(new StepRowText("F5", "from Windows"), StepPlatformMarker.For(F5, HostPlatform.MacOS));
        Assert.Equal(new StepRowText("Wait 30 ms", null), StepPlatformMarker.For(Wait, HostPlatform.MacOS));
    }

    [Fact]
    public void AnOwnVersionReadsAsItself()
    {
        var own = CtrlW with { MacOsOverride = new HotkeyStep(KeyModifiers.Meta | KeyModifiers.Shift, KeyCode.W) };

        Assert.Equal(new StepRowText("Shift+Cmd+W", "own macOS version"), StepPlatformMarker.For(own, HostPlatform.MacOS));
        Assert.Equal(new StepRowText("Ctrl+W", "has macOS version"), StepPlatformMarker.For(own, HostPlatform.Windows));
    }

    [Fact]
    public void ACommandRowSaysWhereItCameFromAndWhatIsMissing()
    {
        Assert.Null(StepPlatformMarker.ForCommand([CtrlW, WinD], HostPlatform.Windows));
        Assert.Equal("from Windows · converted", StepPlatformMarker.ForCommand([CtrlW, Wait], HostPlatform.MacOS));
        Assert.Equal("from Windows · 1 needs a macOS version", StepPlatformMarker.ForCommand([CtrlW, WinD], HostPlatform.MacOS));
        Assert.Equal("from Windows · 2 need a macOS version", StepPlatformMarker.ForCommand([WinD, WinD], HostPlatform.MacOS));
        Assert.Equal("from Windows", StepPlatformMarker.ForCommand([F5, Wait], HostPlatform.MacOS));
    }
}
