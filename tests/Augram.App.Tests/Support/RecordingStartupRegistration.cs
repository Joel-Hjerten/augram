using Augram.Core.Abstractions;

namespace Augram.App.Tests.Support;

/// <summary>An in-memory start-at-login registration that records every write, to prove a development build makes none.</summary>
internal sealed class RecordingStartupRegistration : IStartupRegistration
{
    public RecordingStartupRegistration(bool enabled = false)
    {
        IsEnabled = enabled;
    }

    public bool IsEnabled { get; private set; }

    public List<bool> Writes { get; } = [];

    public void Set(bool enabled)
    {
        Writes.Add(enabled);
        IsEnabled = enabled;
    }
}
