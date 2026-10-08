using System.ComponentModel;

namespace Augram.Platform.Windows.Launch;

/// <summary>
/// The Win32 errors ShellExecuteEx reports, as one line for the step log that names the file, never the arguments:
/// "explorer2 was not found (2)". <see cref="Cancelled"/> (ERROR_CANCELLED) is the declined UAC prompt and is not a
/// failure at all; the launcher turns it into a cancelled result before asking here.
/// </summary>
internal static class ShellExecuteErrors
{
    /// <summary>ERROR_CANCELLED: the user declined the UAC prompt (or another prompt the shell showed).</summary>
    public const int Cancelled = 1223;

    public static string Describe(string file, int error) => error switch
    {
        2 => $"{file} was not found ({error})",
        3 => $"the path of {file} was not found ({error})",
        5 => $"{file}: access denied ({error})",
        267 => $"{file}: the Start in folder is not valid ({error})",
        1155 => $"{file}: no app is associated with it ({error})",
        _ => $"{file} could not be started: {new Win32Exception(error).Message} ({error})",
    };
}
