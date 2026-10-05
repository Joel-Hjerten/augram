using Augram.App.Declarations;

namespace Augram.App.Inspector;

/// <summary>
/// What the F1 inspector copies for a region (ADR-0002 §5d): the declaration path
/// (screen › section › field), the declaration kind, where it was declared, and the bound property.
/// </summary>
public sealed record RegionInfo(string Path, string Kind, SourceLocation Source, string? BoundProperty = null);
