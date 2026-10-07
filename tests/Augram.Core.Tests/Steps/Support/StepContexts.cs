using Augram.Core.Abstractions;
using Augram.Core.Capture;
using Augram.Core.Diagnostics;
using Augram.Core.Steps;

namespace Augram.Core.Tests.Steps.Support;

/// <summary>Builds a <see cref="StepExecutionContext"/> with fakes; every argument has a quiet default.</summary>
internal static class StepContexts
{
    public static readonly CapturePoint Start = new(100, 200, 0);

    public static WindowIdentity Window(string processName = "notepad.exe", nint handle = 0x1234)
        => new(handle, handle, processName, null, "Untitled", ["Notepad"], ProcessId: 42, IsFullScreen: false, IsDesktop: false);

    public static StepExecutionContext Create(
        WindowIdentity? target = null,
        IWindowOperations? windows = null,
        IInputSimulator? input = null,
        IEventLog? log = null,
        CancellationToken cancellation = default,
        bool focusMoved = false)
        => new(
            target,
            Start,
            windows ?? new FakeWindowOperations(),
            input ?? new FakeInputSimulator(),
            log ?? NullEventLog.Instance,
            cancellation)
        {
            FocusMoved = focusMoved,
        };
}
