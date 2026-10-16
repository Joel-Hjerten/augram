namespace Augram.App.Components.StepList;

/// <summary>What a step row reads on this platform (<see cref="StepPlatformMarker.For(Augram.Core.Mapping.CommandStep, Augram.Core.Abstractions.HostPlatform)"/>): its summary line and its F8 marker, null for none.</summary>
public readonly record struct StepRowText(string Summary, string? Marker);
