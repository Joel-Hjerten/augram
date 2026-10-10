using Augram.Core.Abstractions;

namespace Augram.App.Tests.Support;

/// <summary>
/// An in-memory start-at-login registration that records every write (to prove a development build makes none), with a
/// status the test can script (a switch-off in Task Manager, macOS waiting for approval) and an optional failure.
/// </summary>
internal sealed class RecordingStartupRegistration : IStartupRegistration
{
    public RecordingStartupRegistration(bool enabled = false)
        : this(enabled ? StartupStatus.Registered : StartupStatus.NotRegistered)
    {
    }

    public RecordingStartupRegistration(StartupStatus status)
    {
        Status = status;
    }

    public StartupStatus Status
    {
        get => ReadFailure is { } failure ? throw failure : field;
        set;
    }

    public bool IsEnabled => Status == StartupStatus.Registered;

    public List<bool> Writes { get; } = [];

    /// <summary>What a successful register leaves; macOS may answer <see cref="StartupStatus.NeedsApproval"/>.</summary>
    public StartupStatus AfterRegister { get; set; } = StartupStatus.Registered;

    /// <summary>Thrown by <see cref="Set"/> (after recording the write) when set.</summary>
    public Exception? WriteFailure { get; set; }

    /// <summary>Thrown by reading <see cref="Status"/> when set.</summary>
    public Exception? ReadFailure { get; set; }

    public void Set(bool enabled)
    {
        Writes.Add(enabled);
        if (WriteFailure is { } failure)
        {
            throw failure;
        }

        Status = enabled ? AfterRegister : StartupStatus.NotRegistered;
    }
}
