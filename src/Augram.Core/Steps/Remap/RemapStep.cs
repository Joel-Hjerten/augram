using Augram.Core.Abstractions;
using Augram.Core.Steps.Hotkey;

namespace Augram.Core.Steps.Remap;

/// <summary>
/// The one step of a Remap command under a hold remap (F9, plan 0002): while the hold key is held, the command's input is
/// played as <see cref="Output"/>, press for press and release for release (Left held → Middle held, so a drag stays a
/// drag). It is a command's only step (<c>HoldRemaps.HoldRemapRules</c>). The engine worker plays it; the command executor
/// never does (<see cref="RemapStepType.Execute"/> skips it). The same on both platforms: a platform version gives another
/// output where one is needed (F8).
/// </summary>
public sealed record RemapStep(RemapOutput Output) : IStep
{
    public IStepType Type => RemapStepType.Instance;

    /// <summary>"Remap to Ctrl + Middle", "Remap to G", "Remap to Ctrl + wheel up"; "Remap (no key set)" for a key output with no key.</summary>
    public string Summary => SummaryOn(HotkeyText.Names);

    public string SummaryOn(HostPlatform platform) => Output.IsSet ? $"Remap to {Output.Describe(platform)}" : "Remap (no key set)";
}
