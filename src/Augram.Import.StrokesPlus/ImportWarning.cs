namespace Augram.Import.StrokesPlus;

/// <summary>One line of the import report. <paramref name="Item"/> is the source item's name as written in the file.</summary>
public sealed record ImportWarning(ImportSeverity Severity, string Item, string Message);
