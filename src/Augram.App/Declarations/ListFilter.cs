namespace Augram.App.Declarations;

/// <summary>
/// A dropdown above a list that narrows its rows. <see cref="Matches"/> decides per row and selected
/// option; the renderer re-applies every filter when any selection changes.
/// </summary>
public sealed record ListFilter(
    string Label,
    IReadOnlyList<string> Options,
    IValueBinding<string> Selected,
    Func<object, string, bool> Matches);
