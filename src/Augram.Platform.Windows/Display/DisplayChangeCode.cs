namespace Augram.Platform.Windows.Display;

/// <summary>The <c>DISP_CHANGE_*</c> codes of <c>ChangeDisplaySettingsEx</c>, named for the log.</summary>
internal static class DisplayChangeCode
{
    public const int Successful = 0;
    public const int Restart = 1;

    public static string Describe(int code) => code switch
    {
        Successful => "DISP_CHANGE_SUCCESSFUL",
        Restart => "DISP_CHANGE_RESTART: Windows needs a restart for this mode",
        -1 => "DISP_CHANGE_FAILED: the driver could not set the mode",
        -2 => "DISP_CHANGE_BADMODE: the mode is not supported",
        -3 => "DISP_CHANGE_NOTUPDATED: the settings could not be stored",
        -4 => "DISP_CHANGE_BADFLAGS",
        -5 => "DISP_CHANGE_BADPARAM",
        -6 => "DISP_CHANGE_BADDUALVIEW",
        _ => $"code {code}",
    };
}
