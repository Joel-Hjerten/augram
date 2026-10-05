namespace Augram.App.Declarations;

/// <summary>
/// Where a declaration node was written, captured by <c>[CallerFilePath]</c>/<c>[CallerLineNumber]</c>
/// on the node's constructor. The F1 inspector (ADR-0002 §5d) copies it so "move this under that"
/// requests point at a line of code.
/// </summary>
public readonly record struct SourceLocation(string File, int Line)
{
    public override string ToString() => Line > 0 ? $"{File}:{Line}" : File;
}
