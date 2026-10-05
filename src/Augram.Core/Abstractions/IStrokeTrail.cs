using Augram.Core.Capture;

namespace Augram.Core.Abstractions;

/// <summary>
/// The trail overlay as the engine drives it (ADR-0002 §2 <c>IOverlay</c>, F6). Called on the
/// engine worker thread, in this order per stroke: one <see cref="Begin"/>, any number of
/// <see cref="Extend"/>, one <see cref="End"/>. The implementation marshals to its UI thread
/// and returns at once; it must never block the worker.
/// </summary>
public interface IStrokeTrail
{
    void Begin(CapturePoint start);

    void Extend(CapturePoint point);

    void End();
}
