using System.Runtime.CompilerServices;

namespace Augram.App.Declarations;

/// <summary>A titled group of fields on a declared screen (ADR-0002 §5c). Reordering fields is editing the list.</summary>
public sealed record Section(
    string Title,
    IReadOnlyList<Field> Fields,
    string? Help = null,
    [CallerFilePath] string File = "",
    [CallerLineNumber] int Line = 0)
{
    public SourceLocation Source => new(File, Line);
}
