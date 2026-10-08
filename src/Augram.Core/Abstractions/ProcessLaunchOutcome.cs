namespace Augram.Core.Abstractions;

/// <summary>How one <see cref="IProcessLauncher.Launch"/> ended; the Run step turns it into Done, Failed or Skipped.</summary>
public enum ProcessLaunchOutcome
{
    /// <summary>The OS accepted the request (or is still working on it when the adapter's wait ran out).</summary>
    Started,

    /// <summary>The OS refused: not found, no app for the document, access denied, a bad Start in folder.</summary>
    Failed,

    /// <summary>The user said no: the UAC prompt was declined.</summary>
    Cancelled,

    /// <summary>The platform cannot do what was asked (elevation on macOS, no launcher at all).</summary>
    NotSupported,
}
