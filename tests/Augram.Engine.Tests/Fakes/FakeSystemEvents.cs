using Augram.Core.Abstractions;

namespace Augram.Engine.Tests.Fakes;

internal sealed class FakeSystemEvents : ISystemEvents
{
    public event EventHandler<SystemEventKind>? Occurred;

    /// <summary>What the port answers; null (does not say) unless a test sets it.</summary>
    public bool? ReportsForeground { get; set; }

    public void Raise(SystemEventKind kind) => Occurred?.Invoke(this, kind);
}
