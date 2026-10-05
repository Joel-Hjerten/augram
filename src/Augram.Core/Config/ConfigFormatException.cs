namespace Augram.Core.Config;

/// <summary>
/// The JSON is not a configuration this build can read: malformed, missing
/// <c>schemaVersion</c>, or written by a newer Augram. The message says which.
/// </summary>
public sealed class ConfigFormatException : Exception
{
    public ConfigFormatException(string message)
        : base(message)
    {
    }

    public ConfigFormatException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
