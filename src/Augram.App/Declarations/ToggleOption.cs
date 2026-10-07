namespace Augram.App.Declarations;

/// <summary>One check box of a <see cref="TogglesField"/>: its caption beside the box and its own bool binding.</summary>
public sealed record ToggleOption(string Caption, IValueBinding<bool> Value);
