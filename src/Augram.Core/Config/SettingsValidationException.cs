namespace Augram.Core.Config;

/// <summary>A settings value is out of range (see <see cref="SettingsRules"/>). The message is fit to show the user.</summary>
public sealed class SettingsValidationException : Exception
{
    public SettingsValidationException(string message)
        : base(message)
    {
    }
}
