namespace Augram.App.Declarations;

/// <summary>
/// The optional "remove" of a <see cref="CheckListItem"/> (Joel, 2026-10-12: the Not in dialog's unused apps): while
/// <see cref="Removable"/> reads true the item's caption is muted, <see cref="Note"/> follows its detail ("unused") and an ✕
/// after it, its tooltip <see cref="Tip"/>, calls <see cref="Click"/>; while false the item looks like any other. The dialog
/// decides what removing means and rebuilds the list (<see cref="CheckListField.LiveItems"/>).
/// </summary>
public sealed record CheckListRemove(string Note, string Tip, Action Click, IValueBinding<bool> Removable);
