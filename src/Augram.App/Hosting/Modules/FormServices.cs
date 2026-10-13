using Augram.App.Components;
using Augram.App.Components.Capture;
using Augram.Core.Abstractions;
using Augram.Core.Diagnostics;
using Augram.Core.Steps.Hotkey;
using Avalonia;
using Microsoft.Extensions.DependencyInjection;

namespace Augram.App.Hosting.Modules;

/// <summary>
/// Makes the ports that components built without services reach for (step forms and finders are made by assembly scan) an
/// application resource: one <see cref="ComponentPorts"/> under <see cref="ComponentPorts.ResourceKey"/>. The display modes
/// and the window system are only read, so the forms get them under <c>--no-engine</c> too; the engine's key capture (every
/// hotkey field, F5) and button detection (a hold remap command's Add button…, plan 0002 step 4) only while the engine runs,
/// otherwise the fields fall back to their own window. The keyboard layouts port (plan 0011) is only read, so it is published
/// always, and set as <see cref="HotkeyText.Layouts"/> so every key name follows the active layout (decision 6); while the engine
/// runs, a <see cref="SystemEventKind.KeyboardLayoutChanged"/> re-publishes the same ports on the UI thread, which reaches every
/// open hotkey field and Hotkey form as <c>ResourcesChanged</c> (they re-read their names; nothing else is notified).
/// </summary>
public static class FormServices
{
    /// <summary>UI thread, once, right after <see cref="EngineModule.Start"/>.</summary>
    /// <param name="services">The composition root's provider.</param>
    /// <param name="app">The application whose resources the components find.</param>
    /// <param name="engineRuns">What <see cref="EngineModule.Start"/> returned.</param>
    public static void Publish(IServiceProvider services, Application app, bool engineRuns)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(app);
        var layouts = services.GetService<IKeyboardLayouts>() ?? NullKeyboardLayouts.Instance;
        HotkeyText.Layouts = layouts;
        // One Info line so the log says what the names follow (Joel, 2026-10-13: the installed app named Ö ";" while the probe named it "Ö").
        LogLayouts(services.GetRequiredService<IEventLog>(), layouts, "at start");
        var ports = new ComponentPorts
        {
            DisplayModes = services.GetRequiredService<IDisplayModes>(),
            WindowSystem = services.GetRequiredService<IWindowSystem>(),
            KeyCapture = engineRuns ? services.GetRequiredService<IKeyCapture>() : null,
            ButtonCapture = engineRuns ? services.GetRequiredService<IButtonCapture>() : null,
            KeyboardLayouts = layouts,
        };
        app.Resources[ComponentPorts.ResourceKey] = ports;
        if (engineRuns && services.GetService<ISystemEvents>() is { } system)
        {
            // Raised on the OS's thread; the same instance again is a resource change all the same, and that is the notification.
            system.Occurred += (_, kind) =>
            {
                if (kind == SystemEventKind.KeyboardLayoutChanged)
                {
                    UiThread.RunOrPost(() =>
                    {
                        app.Resources[ComponentPorts.ResourceKey] = ports;
                        LogLayouts(services.GetRequiredService<IEventLog>(), layouts, "after a layout change");
                    });
                }
            };
        }
    }

    /// <summary>The active layout, the installed ones and what the Semicolon key (Ö on a Swedish layout) is named, read on the UI thread.</summary>
    private static void LogLayouts(IEventLog log, IKeyboardLayouts layouts, string when)
    {
        var active = layouts.Active;
        log.Info(LogSources.App, "Keyboard layouts", ("when", when), ("adapter", layouts.GetType().Name), ("active", active is null ? "unknown" : active.Id + " (" + active.Name + ")"), ("installed", string.Join(", ", layouts.Installed.Select(layout => layout.Id))), ("semicolonKey", HotkeyText.KeyName(KeyCode.Semicolon)));
    }
}
