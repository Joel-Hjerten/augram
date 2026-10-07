using Augram.Core.Mapping;

namespace Augram.App.Components.CommandTree;

/// <summary>One entry of the command header's Category dropdown: a category of the command's group, or <see cref="Uncategorized"/> (<see cref="Id"/> null).</summary>
public sealed record CategoryChoice(CategoryId? Id, string Name)
{
    public const string UncategorizedName = "Uncategorized";

    public static CategoryChoice Uncategorized { get; } = new(null, UncategorizedName);
}
