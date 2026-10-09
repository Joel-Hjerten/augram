using Augram.App.Declarations;
using Augram.App.Hosting;
using Augram.App.Navigation;
using Augram.App.Screens;
using Augram.App.Tests.Support;
using Augram.App.Tray;
using Augram.App.ViewModels;
using Augram.App.Views;
using Augram.Core.Abstractions;
using Augram.Core.Diagnostics;
using Avalonia.Headless.XUnit;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Augram.App.Tests.Hosting;

/// <summary>
/// "Augram (Dev) 0.2.0" wherever it matters, so there is no mistaking which build runs (Joel, 2026-10-08): the window title, the
/// tray tooltip, Options › About, and the start-at-login toggle that a development build shows disabled.
/// </summary>
public sealed class BuildLabelTests
{
    [Fact]
    public void TheWindowTitleIsTheBuildsNameAndVersion()
    {
        Assert.Equal("Augram (Dev) 0.2.0", new MainWindowViewModel(new NavigationRegistry([]), TestBuilds.Dev).Title);
        Assert.Equal("Augram 0.2.0", new MainWindowViewModel(new NavigationRegistry([]), TestBuilds.Release).Title);
    }

    [AvaloniaFact]
    public void TheMainWindowShowsTheTitleOfThisBuild()
    {
        var window = TestAppBuilder.Services.GetRequiredService<MainWindow>();
        window.Show();

        Assert.Equal(AppInfo.Current.Label, window.Title);
        window.Hide();
    }

    [Fact]
    public void TheTrayTooltipStartsWithTheBuildsNameAndVersion()
    {
        Assert.Equal("Augram (Dev) 0.2.0 (enabled) · synced 14:32", AppTray.ToolTipFor(TestBuilds.Dev, enabled: true, "synced 14:32"));
        Assert.Equal("Augram (Dev) 0.2.0 (disabled)", AppTray.ToolTipFor(TestBuilds.Dev, enabled: false, null));
        Assert.Equal("Augram 0.2.0 (enabled) · synced 14:32", AppTray.ToolTipFor(TestBuilds.Release, enabled: true, "synced 14:32"));
        Assert.Equal("Augram 0.2.0 (disabled)", AppTray.ToolTipFor(TestBuilds.Release, enabled: false, string.Empty));
    }

    [Fact]
    public void ThePausedTrayShowsTheDisabledIcon()
    {
        Assert.True(AppTray.ShowsEnabledIcon(enabled: true, pausedBy: null));
        Assert.False(AppTray.ShowsEnabledIcon(enabled: true, pausedBy: "VMware"));
        Assert.False(AppTray.ShowsEnabledIcon(enabled: false, pausedBy: null));
    }

    [Fact]
    public void TheTrayTooltipSaysWhichFocusedAppPausedAugram()
    {
        Assert.Equal("Augram 0.2.0 (paused: VMware is focused)", AppTray.ToolTipFor(TestBuilds.Release, enabled: true, null, "VMware"));
        Assert.Equal("Augram (Dev) 0.2.0 (paused: VMware is focused) · synced 14:32", AppTray.ToolTipFor(TestBuilds.Dev, enabled: true, "synced 14:32", "VMware"));
        // Switched off wins: a pause means nothing while Augram is disabled anyway.
        Assert.Equal("Augram 0.2.0 (disabled)", AppTray.ToolTipFor(TestBuilds.Release, enabled: false, null, "VMware"));
        Assert.Equal("Augram 0.2.0 (enabled)", AppTray.ToolTipFor(TestBuilds.Release, enabled: true, null, string.Empty));
    }

    [Fact]
    public void OptionsAboutShowsVersionCommitAndChannel()
    {
        using var engine = new EngineFixture(start: false);
        using var vm = Create(engine, TestBuilds.Dev);

        var about = Options(vm).Sections[^1];

        Assert.Equal(OptionsScreen.AboutTitle, about.Title);
        var notes = about.Fields.Cast<NoteField>().Select(field => (field.Label, field.Text.Get())).ToList();
        Assert.Equal([("Version", "0.2.0"), ("Commit", "3f1c2ab"), ("Channel", "Dev (development build)")], notes);

        using var release = Create(engine, TestBuilds.Release);
        Assert.Contains(Options(release).Sections[^1].Fields, field => field is NoteField { Label: "Channel" } note && note.Text.Get() == "Release (installed)");
    }

    [Fact]
    public void ADevBuildShowsStartAtLoginDisabled_WithWhy()
    {
        using var engine = new EngineFixture(start: false);
        using var vm = Create(engine, TestBuilds.Dev);

        var toggle = StartAtLoginToggle(vm);

        Assert.True(toggle.Value.IsReadOnly);
        Assert.Equal("Start at login applies to the installed Augram; this is a development build.", toggle.Help);
        Assert.False(vm.CanChangeStartAtLogin);
    }

    [Fact]
    public void TheInstalledBuildShowsStartAtLoginAsBefore()
    {
        using var engine = new EngineFixture(start: false);
        using var vm = Create(engine, TestBuilds.Release);

        var toggle = StartAtLoginToggle(vm);

        Assert.False(toggle.Value.IsReadOnly);
        Assert.Equal("Also in the tray menu.", toggle.Help);
        toggle.Value.Set(true);
        Assert.True(engine.Settings.Current.General.StartAtLogin);
    }

    private static FormScreen Options(AppSettingsViewModel vm) => Assert.IsType<FormScreen>(OptionsScreen.Declare(vm));

    private static ToggleField StartAtLoginToggle(AppSettingsViewModel vm) =>
        Options(vm).Sections.SelectMany(section => section.Fields).OfType<ToggleField>().Single(field => field.Label == "Start at login");

    private static AppSettingsViewModel Create(EngineFixture engine, AppInfo app)
    {
        var state = new AppState(engine.Settings, new NullStartupRegistration(), NullEventLog.Instance, app);
        var detection = new StrokeButtonDetection(engine.Host, engine.Settings, action => action());
        return new AppSettingsViewModel(engine.Settings, state, detection);
    }
}
