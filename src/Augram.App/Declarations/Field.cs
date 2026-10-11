using Avalonia.Controls;

namespace Augram.App.Declarations;

/// <summary>
/// One row of a declared screen (ADR-0002 §5c): a label, a help line and a binding. Kinds are the
/// derived records in this folder; each has a renderer under <c>Components/Fields/&lt;Kind&gt;/</c>
/// that self-registers in <c>FieldRendererRegistry</c>. <see cref="Kind"/> is the registry key.
/// <see cref="Visible"/>, when set, shows the whole row only while it reads true (Options › Sync's
/// conflicts line); any kind can carry it: <c>new NoteField(…) { Visible = binding }</c>.
/// <see cref="Enabled"/>, when set, enables the row's editor only while it reads true (Options › Appearance's
/// Tint, disabled while the window background is Solid); any kind can carry it, and no renderer needs to know.
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

    /// <summary>
    /// Enables the row's editor only while this reads true; null (the default) leaves it as its renderer made it. The row
    /// stays visible, label and help included. An editor its renderer disabled (a read-only binding) stays disabled.
    /// </summary>
    public IValueBinding<bool>? Enabled { get; init; }

    /// <summary>
    /// The editor takes the row's whole editor column instead of its own width (a pattern row: its text box stretches and the
    /// magnifier and Use Regex sit at the right edge). The row gets the <c>stretch-editor</c> class; the theme does the rest.
    /// </summary>
    public virtual bool StretchesEditor => false;

    /// <summary>Builds the small control shown before the editor, once per rendered row (like <see cref="CustomField.Build"/>); null (the default) for none.</summary>
    public Func<Control>? Accessory { get; init; }

    public SourceLocation Source => new(File, Line);
}
