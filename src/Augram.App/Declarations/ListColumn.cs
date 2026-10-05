namespace Augram.App.Declarations;

/// <summary>One column of a <see cref="ListSpec"/>: header text, how to read a cell, and a width (0 = share the rest).</summary>
public sealed record ListColumn(string Title, Func<object, string> Cell, double Width = 0);
