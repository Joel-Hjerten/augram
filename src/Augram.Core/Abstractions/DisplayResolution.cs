namespace Augram.Core.Abstractions;

/// <summary>
/// A display resolution as that platform's display settings name it: physical pixels on Windows, points (the "looks
/// like" size) on macOS (learnings 0002 §3). Shown as "1920×1080".
/// </summary>
public readonly record struct DisplayResolution(int Width, int Height)
{
    /// <summary>The smallest side a step may store.</summary>
    public const int MinSide = 1;

    /// <summary>The largest side a step may store.</summary>
    public const int MaxSide = 32767;

    public long Area => (long)Width * Height;

    public override string ToString() => $"{Width}×{Height}";
}
