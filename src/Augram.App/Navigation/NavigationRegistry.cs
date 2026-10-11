namespace Augram.App.Navigation;

/// <summary>
/// The ordered tabs of the main window (ADR-0002 §5c: navigation is data). Adding a tab is adding an
/// entry in <c>AppNavigation</c>; the <c>Shell</c> component renders whatever is here.
/// </summary>
public sealed class NavigationRegistry
{
    public NavigationRegistry(IReadOnlyList<NavEntry> entries)
    {
        ArgumentNullException.ThrowIfNull(entries);
        var keys = new HashSet<string>(StringComparer.Ordinal);
        foreach (var entry in Flatten(entries))
        {
            if (!keys.Add(entry.Key))
            {
                throw new ArgumentException($"Duplicate navigation key '{entry.Key}'.", nameof(entries));
            }
        }

        Entries = entries;
    }

    public IReadOnlyList<NavEntry> Entries { get; }

    public NavEntry? Find(string key) => Flatten(Entries).FirstOrDefault(entry => entry.Key == key);

    /// <summary>
    /// The keys a screen hangs on, in tab order: each tab's sub-tabs, or the tab itself when it has none (<c>tools/UiShots</c>
    /// renders one shot per key).
    /// </summary>
    /// <param name="include">Which tabs to take; null takes all.</param>
    public IReadOnlyList<string> LeafKeys(Func<NavEntry, bool>? include = null)
    {
        var keys = new List<string>();
        foreach (var entry in Entries.Where(entry => include?.Invoke(entry) != false))
        {
            if (entry.HasSubEntries)
            {
                keys.AddRange(entry.SubEntries!.Select(sub => sub.Key));
            }
            else
            {
                keys.Add(entry.Key);
            }
        }

        return keys;
    }

    private static IEnumerable<NavEntry> Flatten(IEnumerable<NavEntry> entries)
    {
        foreach (var entry in entries)
        {
            yield return entry;
            if (entry.SubEntries is not null)
            {
                foreach (var sub in Flatten(entry.SubEntries))
                {
                    yield return sub;
                }
            }
        }
    }
}
