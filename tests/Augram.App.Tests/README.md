# Augram.App.Tests

Avalonia headless tests (`Avalonia.Headless.XUnit`) for components and view models, plus `Architecture/` rules on the XAML itself: no `.axaml` under `src/Augram.App/Components/` may contain a literal colour (ADR-0002 §5b), so every component stays re-skinnable through theme tokens.

**May reference:** `Augram.App` (and everything it references), Avalonia.Headless.XUnit, xunit.

Use `[AvaloniaFact]` for anything that touches a control; plain `[Fact]` for file-system and view-model tests. `TestAppBuilder` configures the headless app with the real `CompositionRoot`.
