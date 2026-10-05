namespace Augram.Core.Abstractions;

/// <summary>An in-memory flag: platforms without a login-item adapter yet, and tests.</summary>
public sealed class NullStartupRegistration : IStartupRegistration
{
    public bool IsEnabled { get; private set; }

    public void Set(bool enabled) => IsEnabled = enabled;
}
