namespace Augram.App.Declarations;

/// <summary>
/// A search box above a declared list (plan 0009): <see cref="Text"/> is the box's text (UI-only state on the view model),
/// <see cref="TextOf"/> names the text a row is searched by, and <see cref="Plural"/> the rows' word for the empty state
/// ("No lines match 'xyz'"). The renderer (<c>Components/ItemList</c>) applies <c>Components/SearchBox/SearchQuery</c> to it
/// beside the <see cref="ListFilter"/>s and puts the box after them on the toolbar.
/// </summary>
public sealed record ListSearch(IValueBinding<string> Text, Func<object, string> TextOf, string Plural = "rows");
