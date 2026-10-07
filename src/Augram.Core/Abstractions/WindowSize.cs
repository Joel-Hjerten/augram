namespace Augram.Core.Abstractions;

/// <summary>A window's outer size in physical pixels, for <see cref="WindowOperation.SetSize"/>.</summary>
public readonly record struct WindowSize(int Width, int Height);
