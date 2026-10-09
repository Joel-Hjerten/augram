using Avalonia.Controls;

namespace Augram.App.Declarations;

/// <summary>
/// One row of a declared screen (ADR-0002 §5c): a label, a help line and a binding. Kinds are the
/// derived records in this folder; each has a renderer under <c>Components/Fields/&lt;Kind&gt;/</c>
/// that self-registers in <c>FieldRendererRegistry</c>. <see cref="Kind"/> is the registry key.
/// <see cref="Visible"/>, when set, shows the whole row only while it reads true (Options › Sync's
/// conflicts line); any kind can carry it: <c>new NoteField(…) { Visible = binding }</c>.
/// <see cref="Accessory"/>, when set, puts a small control just before the editor (the window finder's
/// magnifier on the app identification fields); any kind can carry it too.
/// </summary>
public abstract record Field(string Label, string? Help, string File, int Line)
{
    public abstract string Kind { get; }

    /// <summary>The binding behind the field, when it has one; the inspector reports its property name.</summary>
    public abstract IValueBinding? Binding { get; }

    /// <summary>Shows the row only while this reads true; null (the default) always shows it.</summary>
    public IValueBinding<bool>? Visible { get; init; }

    /// <summary>Builds the small control shown before the editor, once per rendered row (like <see cref="CustomField.Build"/>); null (the default) for none.</summary>
    public Func<Control>? Accessory { get; init; }

    public SourceLocation Source => new(File, Line);
}
