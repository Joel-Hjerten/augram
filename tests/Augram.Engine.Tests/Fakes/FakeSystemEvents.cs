using Augram.Core.Abstractions;

namespace Augram.Engine.Tests.Fakes;

internal sealed class FakeSystemEvents : ISystemEvents
{
    public event EventHandler<SystemEventKind>? Occurred;

    public void Raise(SystemEventKind kind) => Occurred?.Invoke(this, kind);
}
