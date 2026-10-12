namespace Augram.App.Declarations;

/// <summary>One button of a <see cref="ButtonsField"/>: its label, what a click does, and when it is enabled (null: always).</summary>
public sealed record FieldButton(string Label, Action Click, IValueBinding<bool>? Enabled = null);
