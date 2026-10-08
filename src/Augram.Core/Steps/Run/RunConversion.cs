using Augram.Core.Abstractions;

namespace Augram.Core.Steps.Run;

/// <summary>
/// A Run step's best guess on the other platform (F8, Joel 2026-10-07: both ways, best effort). A link whose scheme
/// every platform opens the same way (the default browser or mail app) runs unchanged. Everything else has no guess:
/// a program, a path or a bare name belongs to the platform it was written on (<c>explorer</c> is not a Mac program,
/// <c>C:\…</c> is not a Mac path), and so does a platform link such as <c>ms-settings:</c>; those report "needs a … version",
/// so the executor skips the step there and the command gets its own version on that platform. An unset step and a step
/// on its own platform are unchanged.
/// </summary>
internal static class RunConversion
{
    // Schemes the default apps of both platforms open; grows as real use shows more.
    private static readonly HashSet<string> PortableSchemes = new(StringComparer.OrdinalIgnoreCase) { "http", "https", "mailto", "ftp" };

    public static StepConversion Convert(RunStep step, HostPlatform from, HostPlatform to)
    {
        if (from == to || !step.IsSet)
        {
            return StepConversion.Same(step);
        }

        var scheme = ProcessLaunch.SchemeOf(step.File);
        if (scheme is not null && PortableSchemes.Contains(scheme))
        {
            return StepConversion.Same(step);
        }

        var platform = to == HostPlatform.MacOS ? "macOS" : "Windows";
        return StepConversion.None(scheme is null
            ? $"{step.Target} needs a {platform} version: a program and its path belong to one platform"
            : $"{step.Target} needs a {platform} version: {scheme}: links open on one platform only");
    }
}
