using System.Runtime.CompilerServices;

namespace Augram.App.Declarations;

/// <summary>A screen that is a tree of sections and fields, rendered by <c>SectionForm</c>.</summary>
public sealed record FormScreen(
    string Title,
    IReadOnlyList<Section> Sections,
    [CallerFilePath] string File = "",
    [CallerLineNumber] int Line = 0) : ScreenDeclaration(Title, File, Line);
