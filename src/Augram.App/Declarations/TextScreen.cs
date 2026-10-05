using System.Runtime.CompilerServices;

namespace Augram.App.Declarations;

/// <summary>A screen that is a paragraph of text: placeholders for tabs whose content lands in a later step.</summary>
public sealed record TextScreen(
    string Title,
    string Text,
    [CallerFilePath] string File = "",
    [CallerLineNumber] int Line = 0) : ScreenDeclaration(Title, File, Line);
