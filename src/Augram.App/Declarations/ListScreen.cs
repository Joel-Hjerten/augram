using System.Runtime.CompilerServices;

namespace Augram.App.Declarations;

/// <summary>A screen that is one list, rendered by <c>ItemList</c>.</summary>
public sealed record ListScreen(
    string Title,
    ListSpec Spec,
    [CallerFilePath] string File = "",
    [CallerLineNumber] int Line = 0) : ScreenDeclaration(Title, File, Line);
