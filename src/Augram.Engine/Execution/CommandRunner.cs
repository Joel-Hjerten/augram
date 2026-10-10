using System.Diagnostics;
using Augram.Core.Abstractions;
using Augram.Core.Diagnostics;
using Augram.Core.Mapping;
using Augram.Core.Steps;
using Augram.Engine.Hosting;

namespace Augram.Engine.Execution;

/// <summary>
/// Runs one resolved command on the executor thread: plays the active steps in order as they run on this platform
/// (F8: the command's own version for it when it has one, else the original with each step itself where it was
/// authored or its best-guess conversion; a step with no guess is skipped with its reason), sharing one
/// <see cref="StepExecutionContext"/>. The target is
/// activated lazily, right before the first Keyboard, Mouse or Text step, because only injected keys and wheel
/// need focus (the adapter applies A20 and says whether focus moved): a window operation acts on the handle
/// and a media key is global, so a minimize never pays for an activation (2026-10-07: an Alt-tap
/// activation cost 310 ms before a minimize over Chrome). The settle delay (A8) follows that activation
/// only when it moved focus. <c>Failed</c> stops the command, <c>Skipped</c> continues it, and the
/// executor's cancellation stops it after the step that observed it. One Debug line per step, one Info
/// line per command; the last activation outcome is kept for the health summary (B1).
/// </summary>
internal sealed class CommandRunner
{
    private readonly IWindowSystem _windows;
    private readonly IWindowOperations _operations;
    private readonly IDisplayModes _displays;
    private readonly IInputSimulator _simulator;
    private readonly IProcessLauncher _processes;
    private readonly IClipboard _clipboard;
    private readonly IAppActivator _apps;
    private readonly IAppWindow _appWindow;
    private readonly IEventLog _log;
    private readonly int _settleDelayMs;
    private readonly CancellationToken _cancellation;
    private string? _lastActivationOutcome;

    public CommandRunner(EnginePorts ports, int settleDelayMs, CancellationToken cancellation)
    {
        _windows = ports.Windows;
        _operations = ports.WindowOperations;
        _displays = ports.DisplayModes;
        _simulator = ports.Simulator;
        _processes = ports.ProcessLauncher;
        _clipboard = ports.Clipboard;
        _apps = ports.Apps;
        _appWindow = ports.AppWindow;
        _log = ports.Log;
        _settleDelayMs = settleDelayMs;
        _cancellation = cancellation;
    }

    /// <summary>"technique (N ms)" of the last activation that was needed, or "failed: technique"; null until one happened.</summary>
    public string? LastActivationOutcome => Volatile.Read(ref _lastActivationOutcome);

    public void Run(ExecutionRequest request, AppGroup group, Command command, WindowIdentity? target)
    {
        var plan = command.PlanFor(_operations.Platform);
        if (!plan.Any(step => step.Stored.IsActive))
        {
            _log.Info(LogSources.Execution, "Command has no active steps", ("command", CommandNames.Label(group, command)));
            return;
        }

        var context = new StepExecutionContext(target, request.Start, _operations, _simulator, _log, _cancellation) { Processes = _processes, Displays = _displays, Clipboard = _clipboard, Apps = _apps, AppWindow = _appWindow };
        var activated = false;
        var run = 0;
        var skipped = 0;
        for (var index = 0; index < plan.Count; index++)
        {
            var (commandStep, planned) = plan[index];
            if (!commandStep.IsActive)
            {
                continue;
            }

            if (planned.Step is not { } step)
            {
                // F8: authored on the other platform with no sensible guess here ("Win+D needs a macOS version").
                skipped++;
                _log.Debug(LogSources.Execution, "Step skipped", ("index", index), ("type", commandStep.Step.Type.Key), ("reason", planned.Reason));
                continue;
            }

            if (!activated && NeedsFocus(step))
            {
                activated = true;
                var focusMoved = Activate(target);
                context = context with { FocusMoved = focusMoved };
                if (focusMoved && !Settle())
                {
                    LogCancelled(group, command, index);
                    return;
                }
            }

            var started = Stopwatch.GetTimestamp();
            var result = step.Type.Execute(step, context);
            _log.Debug(
                LogSources.Execution,
                "Step ran",
                ("index", index),
                ("type", step.Type.Key),
                ("summary", step.LogSummary),
                ("converted", planned.Kind == StepConversionKind.Converted),
                ("outcome", result.Outcome),
                ("reason", result.Reason),
                ("ms", Math.Round(Stopwatch.GetElapsedTime(started).TotalMilliseconds, 2)));

            if (result.Outcome == StepOutcome.Failed)
            {
                _log.Warning(LogSources.Execution, "Command stopped", ("command", CommandNames.Label(group, command)), ("step", index), ("type", step.Type.Key), ("summary", step.LogSummary), ("reason", result.Reason));
                return;
            }

            if (_cancellation.IsCancellationRequested)
            {
                LogCancelled(group, command, index);
                return;
            }

            if (result.Outcome == StepOutcome.Skipped)
            {
                skipped++;
            }
            else
            {
                run++;
            }
        }

        _log.Info(
            LogSources.Execution,
            "Command fired",
            ("command", CommandNames.Label(group, command)),
            ("trigger", request.Describe()),
            ("stepsRun", run),
            ("stepsSkipped", skipped),
            ("elapsedMs", Math.Round(Stopwatch.GetElapsedTime(request.EnqueuedAt).TotalMilliseconds, 1)),
            ("process", target?.ProcessName));
    }

    /// <summary>Activation per A20 is the adapter's call; here: log it, remember it for health, and say whether focus moved. A failure is not fatal: the command runs on whatever is in front.</summary>
    private bool Activate(WindowIdentity? target)
    {
        if (target is null)
        {
            return false;
        }

        var result = _windows.Activate(target);
        var focusMoved = result.Succeeded && result.Technique != ActivationResult.NotNeeded.Technique;
        if (result.Succeeded)
        {
            _log.Info(LogSources.Execution, "Window activated", ("technique", result.Technique), ("elapsedMs", result.ElapsedMs), ("focusMoved", focusMoved), ("process", target.ProcessName));
        }
        else
        {
            _log.Warning(LogSources.Execution, "Activation failed", ("technique", result.Technique), ("elapsedMs", result.ElapsedMs), ("process", target.ProcessName));
        }

        if (focusMoved || !result.Succeeded)
        {
            Volatile.Write(ref _lastActivationOutcome, result.Succeeded ? $"{result.Technique} ({result.ElapsedMs} ms)" : $"failed: {result.Technique}");
        }

        return focusMoved;
    }

    /// <summary>
    /// Injected keys land in whatever has focus, so Keyboard and Text steps need the target in front; so do Mouse steps
    /// (Scroll), whose held keys go to the foreground window and whose wheel must reach the same one.
    /// </summary>
    private static bool NeedsFocus(IStep step) => step.Type.Category is StepCategory.Keyboard or StepCategory.Mouse or StepCategory.Text;

    /// <summary>A8: the one wait, cancellation-aware; false when the executor is stopping.</summary>
    private bool Settle()
    {
        if (_settleDelayMs <= 0)
        {
            return !_cancellation.IsCancellationRequested;
        }

        return !_cancellation.WaitHandle.WaitOne(_settleDelayMs);
    }

    private void LogCancelled(AppGroup group, Command command, int index)
        => _log.Info(LogSources.Execution, "Command cancelled", ("command", CommandNames.Label(group, command)), ("step", index));
}
