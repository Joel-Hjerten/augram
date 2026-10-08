using Augram.Core.Abstractions;

namespace Augram.Core.Steps.Hdr;

/// <summary>
/// Switches a display's HDR (the system switch behind Settings' "Use HDR"; Joel's Win+Alt+B hotkey stays the other
/// way). Windows only: macOS offers apps no way to switch HDR, so the type converts to "no equivalent" there.
/// </summary>
public sealed record HdrStep(HdrAction Action = HdrAction.Toggle, DisplayTarget Target = DisplayTarget.UnderGesture) : IStep
{
    public IStepType Type => HdrStepType.Instance;

    /// <summary>"Toggle HDR", "HDR on", "HDR off", with " (main display)" when targeted.</summary>
    public string Summary => Target == DisplayTarget.Main ? $"{What} (main display)" : What;

    private string What => Action switch
    {
        HdrAction.On => "HDR on",
        HdrAction.Off => "HDR off",
        _ => "Toggle HDR",
    };
}
