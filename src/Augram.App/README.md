# Augram.App

The Avalonia desktop app: tray, settings workbench, learn dialog, overlay, shared components, dev gallery. MVVM with `CommunityToolkit.Mvvm`; views are XAML plus a near-empty code-behind; view models are disposable projections over Core/Engine stores (ADR-0002 §5, §5a).

**May reference:** `Augram.Core`, `Augram.Engine`, `Augram.Platform.Windows`, `Augram.Platform.MacOS`, and the Avalonia / CommunityToolkit.Mvvm / Microsoft.Extensions.DependencyInjection packages. `CompositionRoot.cs` is the one place services are registered; nothing else calls `new` on a service.

**Layout:** `Views/` screens (compose components, own no visuals), `Components/` one folder per shared lookless control (no literal colours; enforced by `tests/Augram.App.Tests/Architecture`), `Themes/` token dictionaries (Wireframe first), `DevGallery/` Debug-only.

**Must never contain:** business rules, application state that outlives a window, P/Invoke, or SharpHook.
