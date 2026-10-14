namespace Augram.App.Components.StepList;

/// <summary>
/// A command row's small platform badge (Joel, 0.19.7: the F8 line was in the way): which platforms the command has steps for
/// ("Win", "Mac", "Win + Mac"), and whether something there wants a look (a step with no counterpart here, an own version made
/// before the original changed). The sentence it stands for is its tooltip and the Steps for row's note (<see cref="StepPlatformMarker"/>).
/// </summary>
public readonly record struct PlatformBadge(string Text, bool NeedsAttention);
