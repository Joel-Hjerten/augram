using Augram.Core.HoldRemaps;
using Augram.Core.Mapping;

namespace Augram.App.ViewModels.Commands;

/// <summary>A hold remap as copied to the <see cref="CommandClipboard"/>, with the commands under it then (plan 0002).</summary>
public sealed record HoldRemapCopy(HoldRemap HoldRemap, IReadOnlyList<Command> Commands);
