namespace Augram.Core.Mapping;

/// <summary>A group, command or ignored app broke a mapping rule (name empty or taken, trigger bound twice, invalid pattern, Global missing or removed). The message is fit to show the user.</summary>
public sealed class MappingValidationException : Exception
{
    public MappingValidationException(string message)
        : base(message)
    {
    }
}
