using System.Reflection;
using System.Text.RegularExpressions;
using Augram.Core.Abstractions;
using Augram.Engine.Hosting;
using Xunit;
using static Augram.App.Tests.Architecture.SourceScan;

namespace Augram.App.Tests.Architecture;

/// <summary>
/// One home per cross-project job (plan 0007 S18; docs/reference/one-home-guards.md): which platform this is and its name, log
/// sources, keys held around an injection, and which project may reference which.
/// </summary>
public sealed partial class OneHomePlatformTests
{
    private const string App = "src/Augram.App/";

    private static IEnumerable<string> AllCode => Code("src", "tools");

    [Fact]
    public void PlatformChecksGoThroughRunningPlatform()
        => AssertOnlyIn(
            OperatingSystemCheck(),
            AllCode,
            "Ask Core/Abstractions/RunningPlatform.Current (tests can set it) instead of OperatingSystem.IsWindows/IsMacOS; the Engine asks EnginePorts.Platform (plan 0007 S18). "
            + "The operating system itself is asked only where a library, the toolkit or the OS needs the real answer (allowlisted).",
            new Allowed("src/Augram.Core/Abstractions/RunningPlatform.cs", "the home"),
            new Allowed("src/Augram.Core/Config/DefaultConfigFolder.cs", "the OS's own folder convention, whatever the process reads as"),
            new Allowed("src/Augram.Sync.Git/RemoteUrl.cs", "the file system's case rule for a local path"),
            new Allowed("src/Augram.Engine/Input/", "SharpHook's per-OS quirks (Engine README)"),
            new Allowed(App + "App.axaml.cs", "the macOS launch event and Dock, observed before the main loop"),
            new Allowed(App + "App.Launch.cs", "the macOS launch event"),
            new Allowed(App + "Hosting/Modules/", "picks the Windows or macOS adapters (the analyzer's platform guard)"),
            new Allowed(App + "Hosting/Instance/", "the OS's pipe and mutex semantics"),
            new Allowed(App + "Views/MainWindow.axaml.cs", "window chrome: the macOS title bar's padding"),
            new Allowed(App + "Themes/WindowBackdrop.cs", "the toolkit's transparency levels per OS"),
            new Allowed(App + "Overlay/TrailOverlayWindow.cs", "the toolkit's coordinates and screens per OS"),
            new Allowed(App + "Components/GestureDrawArea/GestureDrawArea.cs", "the toolkit's render scaling on macOS"),
            new Allowed(App + "Tray/", "the native tray or menu-bar item per OS"));

    [Fact]
    public void OnlyHostPlatformExtensionsNamesAPlatform()
        => AssertOnlyIn(
            PlatformName(),
            AllCode,
            "Use HostPlatform.DisplayName() (Core/Abstractions/HostPlatformExtensions) instead of a \"Windows\" or \"macOS\" literal (plan 0007 S18).",
            new Allowed("src/Augram.Core/Abstractions/HostPlatformExtensions.cs", "the home"));

    [Fact]
    public void LogSourcesAreNeverLiterals()
        => AssertOnlyIn(
            LiteralLogSource(),
            AllCode,
            "Pass a Core/Diagnostics/LogSources constant as the source; a new source is a constant there and an entry in LogSources.All (plan 0007 S18).");

    [Fact]
    public void KeysAreHeldAroundAnInjectionOnlyThroughInputSimulation()
        => AssertOnlyIn(
            KeyLoop(),
            AllCode,
            "Use Core/Abstractions/InputSimulation (WithModifiersHeld, or PressKeys and ReleaseKeys for a hold that spans two calls) instead of a loop pressing keys (plan 0007 S18).",
            new Allowed("src/Augram.Core/Abstractions/InputSimulation.cs", "the home"),
            new Allowed("src/Augram.Core/Steps/Scroll/ScrollExecutor.cs", "leftover, fold later: stops at the first failed press and names the key in the failure (A19), which InputSimulation does not"));

    [Fact]
    public void ProjectsReferenceOnlyWhatTheLayeringAllows()
    {
        // ADR-0002 Â§1: Core references no Augram project and no UI toolkit; Engine, the Platform adapters, sync and import only Core;
        // nothing but the App (and the tools) references Avalonia. Core/Training's session is UI-free for this reason (S12).
        Assembly[] adapters = [typeof(EngineHost).Assembly, typeof(Augram.Platform.Windows.Input.Win32CursorProbe).Assembly, typeof(Augram.Platform.MacOS.Input.MacCursorProbe).Assembly, typeof(Augram.Sync.Git.GitSyncRepository).Assembly, typeof(Augram.Import.StrokesPlus.StrokesPlusImporter).Assembly];
        var offenders = new List<string>();
        offenders.AddRange(Forbidden(typeof(RunningPlatform).Assembly, name => name.StartsWith("Augram.", StringComparison.Ordinal) || name.StartsWith("Avalonia", StringComparison.Ordinal)));
        foreach (var adapter in adapters)
        {
            offenders.AddRange(Forbidden(adapter, name => (name.StartsWith("Augram.", StringComparison.Ordinal) && name != "Augram.Core") || name.StartsWith("Avalonia", StringComparison.Ordinal)));
        }

        Assert.True(offenders.Count == 0, "References the layering forbids (ADR-0002 Â§1; move the code to the project that owns the job):" + Environment.NewLine + string.Join(Environment.NewLine, offenders));
    }

    private static IEnumerable<string> Forbidden(Assembly assembly, Func<string, bool> forbidden)
        => assembly.GetReferencedAssemblies().Select(reference => reference.Name!).Where(forbidden).Select(name => $"{assembly.GetName().Name} -> {name}");

    [GeneratedRegex(@"\bOperatingSystem\.Is(?:Windows|MacOS|Linux)\b|\bRuntimeInformation\.IsOSPlatform\b")]
    private static partial Regex OperatingSystemCheck();

    [GeneratedRegex(@"""(?:Windows|macOS)""")]
    private static partial Regex PlatformName();

    // log.Info("sync", â€¦), and a LogEvent built with a literal source.
    [GeneratedRegex(@"\.(?:Trace|Debug|Info|Warning|Error)\(\s*\$?@?""|\bnew\s+LogEvent\([^,]*,[^,]*,\s*\$?@?""")]
    private static partial Regex LiteralLogSource();

    // A for/foreach whose next few lines press a key: the opening of a hand-made bracket. (A loop that only releases, such as
    // the hold remap's reset of every key still down, is not one.)
    [GeneratedRegex(@"\b(?:for|foreach)\s*\([^\n]*\n(?:[^\n]*\n){0,3}?[^\n]*\.KeyPress\(")]
    private static partial Regex KeyLoop();
}
