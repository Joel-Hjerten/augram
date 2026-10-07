namespace Augram.Core.Steps;

/// <summary>A step's stored parameters cannot be read (F8); the message names the member and is fit for the config notice.</summary>
public sealed class StepFormatException : Exception
{
    public StepFormatException()
    {
    }

    public StepFormatException(string message)
        : base(message)
    {
    }

    public StepFormatException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
