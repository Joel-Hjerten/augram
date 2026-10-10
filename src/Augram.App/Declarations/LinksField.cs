using System.Runtime.CompilerServices;

namespace Augram.App.Declarations;

/// <summary>
/// Links one under the other, each opening something elsewhere in the app: an Exclusions › Per command entry's "Used by", the
/// commands that name it (plan 0004), each opening its command on the Commands tab. The list follows its binding, so it
/// changes while shown; with none it shows <see cref="EmptyText"/>.
/// </summary>
public sealed record LinksField(
    string Label,
    IValueBinding<IReadOnlyList<LinkItem>> Links,
    string EmptyText,
    string? Help = null,
    [CallerFilePath] string File = "",
    [CallerLineNumber] int Line = 0) : Field(Label, Help, File, Line)
{
    public override string Kind => "Links";

    public override IValueBinding Binding => Links;
}
