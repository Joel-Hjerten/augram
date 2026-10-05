using Augram.App.Declarations;

namespace Augram.App.Navigation;

/// <summary>
/// One tab (F7). An entry either shows a screen or holds sub-entries that become sub-tabs. The screen
/// is a factory so a tab's view model is created when the shell builds, not when the registry is declared.
/// </summary>
public sealed record NavEntry(
    string Title,
    string Key,
    Func<ScreenDeclaration>? Screen = null,
    IReadOnlyList<NavEntry>? SubEntries = null)
{
    public bool HasSubEntries => SubEntries is { Count: > 0 };
}
