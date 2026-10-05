namespace Augram.Core.Abstractions;

/// <summary>Lifecycle transitions an <see cref="IInputSource"/> reports (N4: hook installed / lost / reinstalled).</summary>
public enum HookHealthKind
{
    /// <summary>The hook is installed and the OS has started delivering to it.</summary>
    Installed,

    /// <summary>The hook stopped because <see cref="IInputSource.Stop"/> was called. Expected; no reinstall.</summary>
    Stopped,

    /// <summary>The OS disabled the hook or the hook loop failed without a <see cref="IInputSource.Stop"/>. Needs a reinstall.</summary>
    Lost,
}
