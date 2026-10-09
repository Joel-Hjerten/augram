using Augram.App.Hosting;
using Augram.App.Tests.Support;
using Augram.App.Tray;
using Augram.Core.Abstractions;
using Augram.Core.Diagnostics;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Xunit;

namespace Augram.App.Tests.Tray;

/// <summary>
/// The first click shows the toggled icon at once while it waits to become a single or a double (Joel, 2026-10-09: snappier);
/// a double puts the icon back and opens the window, and the setting itself only changes when the single is decided.
/// </summary>
public sealed class TrayClickPreviewTests
{
    [AvaloniaFact]
    public void TheFirstClickShowsTheToggledIconAtOnce_AndADoublePutsItBack()
    {
        using var engine = new EngineFixture(start: false);
        using var state = new AppState(engine.Settings, new NullStartupRegistration(), NullEventLog.Instance, TestBuilds.Dev);
        var host = new FakeTrayHost();
        var opened = 0;
        using var tray = new AppTray(state, () => opened++, () => { }, NullEventLog.Instance, null, null, _ => host);
        var enabledIcon = host.Icon;
        Assert.True(state.Enabled);

        host.Click();
        Assert.NotSame(enabledIcon, host.Icon);
        Assert.True(state.Enabled);
        Assert.Contains("(enabled)", host.ToolTip, StringComparison.Ordinal);

        host.Click();
        Assert.Same(enabledIcon, host.Icon);
        Assert.True(state.Enabled);
        Assert.Equal(1, opened);
    }

    private sealed class FakeTrayHost : ITrayHost
    {
        public event EventHandler? Clicked;

        public WindowIcon? Icon { get; private set; }

        public string ToolTip { get; private set; } = string.Empty;

        public void Click() => Clicked?.Invoke(this, EventArgs.Empty);

        public void Show(WindowIcon icon, bool isTemplate, string toolTip)
        {
            Icon = icon;
            ToolTip = toolTip;
        }

        public void Dispose()
        {
        }
    }
}
