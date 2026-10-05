namespace Augram.Import.StrokesPlus;

/// <summary>The input is not a StrokesPlus.net JSON document (malformed JSON or not an object).</summary>
public sealed class ImportFormatException : Exception
{
    public ImportFormatException()
    {
    }

    public ImportFormatException(string message)
        : base(message)
    {
    }

    public ImportFormatException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
