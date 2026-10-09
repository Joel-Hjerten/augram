using System.Collections;
using Augram.Core.Gestures;
using Augram.Core.Mapping;

namespace Augram.Core.Sync;

/// <summary>
/// A gesture set and mapping as <see cref="SyncItem"/>s, in document order (gestures in library order, then
/// each group's header, categories, hold remaps and commands, each command followed by its own version when it has one,
/// then the ignored apps), one item per key. Immutable.
/// </summary>
public sealed class SyncItemSet : IReadOnlyCollection<SyncItem>
{
    private readonly SyncItem[] _items;
    private readonly Dictionary<SyncItemKey, SyncItem> _byKey;

    /// <exception cref="ArgumentException">Two items share a key.</exception>
    public SyncItemSet(IEnumerable<SyncItem> items)
    {
        ArgumentNullException.ThrowIfNull(items);
        _items = items.ToArray();
        _byKey = new Dictionary<SyncItemKey, SyncItem>(_items.Length);
        foreach (var item in _items)
        {
            if (!_byKey.TryAdd(item.Key, item))
            {
                throw new ArgumentException($"Two sync items share the key {item.Key}.", nameof(items));
            }
        }
    }

    public static SyncItemSet Empty { get; } = new([]);

    public int Count => _items.Length;

    /// <summary>Splits a validated gesture list and mapping into items.</summary>
    public static SyncItemSet From(IReadOnlyList<Gesture> gestures, MappingDocument mapping)
    {
        ArgumentNullException.ThrowIfNull(gestures);
        ArgumentNullException.ThrowIfNull(mapping);

        var items = new List<SyncItem>();
        items.AddRange(gestures.Select(gesture => new SyncItem.GestureItem(gesture)));
        foreach (var group in mapping.Groups)
        {
            items.Add(new SyncItem.GroupItem(group));
            items.AddRange(group.Categories.Select(category => new SyncItem.CategoryItem(group.Id, category)));
            items.AddRange(group.HoldRemaps.Select(holdRemap => new SyncItem.HoldRemapItem(group.Id, holdRemap)));
            foreach (var command in group.Commands)
            {
                items.Add(new SyncItem.CommandItem(group.Id, command));
                if (command.OwnVersion is { } own)
                {
                    items.Add(new SyncItem.VersionItem(command.Id, command.Name, own));
                }
            }
        }

        items.AddRange(mapping.Ignored.Select(app => new SyncItem.IgnoredItem(app)));
        return new SyncItemSet(items);
    }

    public SyncItem? Find(SyncItemKey key) => _byKey.GetValueOrDefault(key);

    public bool Contains(SyncItemKey key) => _byKey.ContainsKey(key);

    /// <summary>The content of each item by key: what a merge base or a published revision stores.</summary>
    public IReadOnlyDictionary<SyncItemKey, string> Contents() => _byKey.ToDictionary(pair => pair.Key, pair => pair.Value.Content);

    public int CountOf(SyncItemKind kind) => _items.Count(item => item.Kind == kind);

    /// <summary>True when both sets hold the same keys with the same contents (order aside).</summary>
    public bool SameAs(IReadOnlyDictionary<SyncItemKey, string> contents)
    {
        ArgumentNullException.ThrowIfNull(contents);
        return contents.Count == _byKey.Count
            && contents.All(pair => _byKey.TryGetValue(pair.Key, out var item) && string.Equals(item.Content, pair.Value, StringComparison.Ordinal));
    }

    public IEnumerator<SyncItem> GetEnumerator() => ((IEnumerable<SyncItem>)_items).GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}
