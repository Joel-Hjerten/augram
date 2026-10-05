using Augram.Core.Capture;

namespace Augram.Core.Abstractions;

/// <summary>No overlay: the default until the App wires one, and the choice for headless tests.</summary>
public sealed class NullStrokeTrail : IStrokeTrail
{
    private NullStrokeTrail()
    {
    }

    public static NullStrokeTrail Instance { get; } = new();

    public void Begin(CapturePoint start)
    {
    }

    public void Extend(CapturePoint point)
    {
    }

    public void End()
    {
    }
}
