namespace Augram.Core.Gestures;

/// <summary>A gesture broke a library rule (name empty or taken, no usable sample, unknown or duplicate id). The message is fit to show the user.</summary>
public sealed class GestureValidationException : Exception
{
    public GestureValidationException(string message)
        : base(message)
    {
    }
}
