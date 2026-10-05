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
