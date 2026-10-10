namespace Augram.App.Declarations;

/// <summary>
/// One link of a <see cref="LinksField"/>: its caption ("Global › Media › Zoom in"), what a click does (open the command), and
/// a muted detail after it ("inactive"), or none. Without <see cref="Open"/> it shows as plain text.
/// </summary>
public sealed record LinkItem(string Caption, Action? Open, string? Detail = null);
