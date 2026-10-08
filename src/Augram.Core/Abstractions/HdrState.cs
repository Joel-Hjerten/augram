namespace Augram.Core.Abstractions;

/// <summary>A display's HDR as its platform adapter reads it (learnings 0002 §2–3).</summary>
public enum HdrState
{
    /// <summary>The display cannot do HDR, or the platform cannot tell (macOS has no public API for it).</summary>
    Unsupported,

    Off,

    On,
}
