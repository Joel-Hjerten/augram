using Augram.App.Components;
using Augram.App.Components.Capture;
using Augram.Core.Abstractions;
using Avalonia;
using Microsoft.Extensions.DependencyInjection;

namespace Augram.App.Hosting.Modules;

/// <summary>
/// Makes the ports that components built without services reach for (step forms and finders are made by assembly scan) an
/// application resource: one <see cref="ComponentPorts"/> under <see cref="ComponentPorts.ResourceKey"/>. The display modes
/// and the window system are only read, so the forms get them under <c>--no-engine</c> too; the engine's key capture (every
/// hotkey field, F5) and button detection (a hold remap command's Add button…, plan 0002 step 4) only while the engine runs,
/// otherwise the fields fall back to their own window.
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
        app.Resources[ComponentPorts.ResourceKey] = new ComponentPorts
        {
            DisplayModes = services.GetRequiredService<IDisplayModes>(),
            WindowSystem = services.GetRequiredService<IWindowSystem>(),
            KeyCapture = engineRuns ? services.GetRequiredService<IKeyCapture>() : null,
            ButtonCapture = engineRuns ? services.GetRequiredService<IButtonCapture>() : null,
        };
    }
}
