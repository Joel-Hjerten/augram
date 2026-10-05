using System.Runtime.CompilerServices;
using Avalonia.Controls;

namespace Augram.App.Declarations;

/// <summary>
/// A screen that is one shared component filling the tab (the Gestures grid). <see cref="Build"/>
/// creates the component and binds it to the screen's view model; the component itself stays
/// presentational (ADR-0002 §5a, §5c). Screens needing sections or a list use the other shapes.
/// </summary>
public sealed record ComponentScreen(
    string Title,
    Func<Control> Build,
    [CallerFilePath] string File = "",
    [CallerLineNumber] int Line = 0) : ScreenDeclaration(Title, File, Line);
