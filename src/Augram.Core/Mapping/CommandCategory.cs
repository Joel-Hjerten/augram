namespace Augram.Core.Mapping;

/// <summary>
/// A named section a group's commands are sorted into (Joel, 2026-10-07; SP.net's per-application
/// "Categories"). The Global group uses them as its organizing principle, the way app groups organize
/// app commands: the Commands › Global tab shows one collapsible section per category. A command
/// points at one by <see cref="Command.CategoryId"/>; none means "Uncategorized". Names are unique
/// within a group and sorted by name like everything else in F5a.
/// </summary>
public sealed record CommandCategory(CategoryId Id, string Name);
