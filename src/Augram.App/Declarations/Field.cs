namespace Augram.App.Declarations;

/// <summary>
/// One row of a declared screen (ADR-0002 §5c): a label, a help line and a binding. Kinds are the
/// derived records in this folder; each has a renderer under <c>Components/Fields/&lt;Kind&gt;/</c>
/// that self-registers in <c>FieldRendererRegistry</c>. <see cref="Kind"/> is the registry key.
/// <see cref="Visible"/>, when set, shows the whole row only while it reads true (Options › Sync's
/// conflicts line); any kind can carry it: <c>new NoteField(…) { Visible = binding }</c>.
/// </summary>
public abstract record Field(string Label, string? Help, string File, int Line)
{
    public abstract string Kind { get; }

    /// <summary>The binding behind the field, when it has one; the inspector reports its property name.</summary>
    public abstract IValueBinding? Binding { get; }

    /// <summary>Shows the row only while this reads true; null (the default) always shows it.</summary>
    public IValueBinding<bool>? Visible { get; init; }

    public SourceLocation Source => new(File, Line);
}
