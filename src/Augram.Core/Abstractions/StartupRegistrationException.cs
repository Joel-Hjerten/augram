namespace Augram.Core.Abstractions;

/// <summary>
/// The OS refused to change start at login for a reason of its own (macOS: an <c>NSError</c> from <c>SMAppService</c>, or no
/// <c>SMAppService</c> at all). The message is the OS's text; the App logs it like a registry failure.
/// </summary>
public sealed class StartupRegistrationException : Exception
{
    public StartupRegistrationException()
    {
    }

    public StartupRegistrationException(string message)
        : base(message)
    {
    }

    public StartupRegistrationException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
