using Augram.Core.Config;

namespace Augram.App.Declarations;

/// <summary>One round swatch a <see cref="ColorField"/> offers (plan 0006 decision 8): its name (the swatch's tooltip) and colour.</summary>
public sealed record ColourPreset(string Name, RgbColor Colour);
