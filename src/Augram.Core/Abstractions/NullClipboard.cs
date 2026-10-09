namespace Augram.Core.Abstractions;

/// <summary>Touches no clipboard and says so; the default for tests and for a platform without an adapter.</summary>
public sealed class NullClipboard : IClipboard
{
    public const string Reason = "no clipboard on this platform";

    public static NullClipboard Instance { get; } = new();

    private NullClipboard()
    {
    }

    public ClipboardResult Clear() => ClipboardResult.NotSupported(Reason);
}
