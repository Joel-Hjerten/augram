namespace Augram.Core.Transfer;

/// <summary>
/// A file item that took a local item's id because it matched it by name, hold key or shape (plan 0003, decision 5), for
/// the review ("'Zig' in the file has the shape of your 'Zag', 96"). <paramref name="FileName"/> is its name in the file;
/// <paramref name="Score"/> is the shape score, null for the other kinds.
/// </summary>
public sealed record ImportMatch(ImportMatchKind By, string FileName, double? Score = null);
