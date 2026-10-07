using Augram.App.Declarations;

namespace Augram.App.Components.FormDialog;

/// <summary>
/// What a <see cref="FormDialog"/> shows: a title, the confirm button's label ("Create", "Delete"),
/// an optional message line (a confirmation question) and an optional declared form (the app group
/// form). Either part may be absent; a confirmation is a message alone.
/// </summary>
public sealed record FormDialogRequest(string Title, string ConfirmLabel, string? Message = null, FormScreen? Screen = null);
