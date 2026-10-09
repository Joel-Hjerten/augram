using System.Threading.Channels;
using Augram.Core.Abstractions;
using Augram.Core.Capture;
using Augram.Core.Diagnostics;
using Augram.Engine.Hosting;
using Xunit;

namespace Augram.Engine.Tests.Input;

/// <summary>
/// A19 with the ignore list: the real <see cref="InputGate"/> decides on random sequences while the watch's answer (over an
/// ignored app, paused by a focused one) and the tray toggle flip between any two events, and this test plays the worker
/// (drains the gate's queue into a <see cref="CaptureStateMachine"/> and publishes its state back). Every stroke-button
/// press and its release get the same decision, and every hook decision equals the machine's.
/// </summary>
public sealed class IgnorePairingTests
{
    private const int Sequences = 2000;
    private static readonly MouseButton[] Buttons = Enum.GetValues<MouseButton>();

    [Fact]
    public void RandomSequences_APressAndItsReleaseAgree_AndTheHookAgreesWithTheMachine()
    {
        var rng = new Random(20261009);
        var passedForTheIgnoreList = 0;
        var consumed = 0;

        for (var sequence = 0; sequence < Sequences; sequence++)
        {
            var strokeButton = Buttons[rng.Next(Buttons.Length)];
            var queue = Channel.CreateUnbounded<WorkerMessage>();
            var gate = new InputGate(queue.Writer, NullEventLog.Instance, strokeButton, KeyModifiers.Control, enabled: true);
            var machine = new CaptureStateMachine(strokeButton);
            var pressDecision = new bool?[Buttons.Length];

            foreach (var raw in Generate(rng))
            {
                if (rng.Next(3) == 0)
                {
                    gate.PublishIgnore(rng.Next(4));
                }

                if (rng.Next(15) == 0)
                {
                    gate.Enabled = !gate.Enabled;
                }

                var answerBefore = gate.IgnoreState;
                var suppressed = gate.Handle(in raw);
                Drain(queue.Reader, gate, machine, sequence);

                if (raw.Kind == RawInputKind.ButtonDown && raw.Button == strokeButton)
                {
                    pressDecision[(int)raw.Button] = suppressed;
                    consumed += suppressed ? 1 : 0;
                    passedForTheIgnoreList += !suppressed && answerBefore != 0 && gate.Enabled && (raw.Modifiers & KeyModifiers.Control) == 0 ? 1 : 0;
                }
                else if (raw.Kind == RawInputKind.ButtonUp && pressDecision[(int)raw.Button] is { } press)
                {
                    Assert.True(press == suppressed, $"sequence {sequence}: {raw.Button} pressed {(press ? "consumed" : "passed")}, released {(suppressed ? "consumed" : "passed")}");
                    pressDecision[(int)raw.Button] = null;
                }
            }
        }

        Assert.True(passedForTheIgnoreList > 500, $"only {passedForTheIgnoreList} presses passed for the ignore list");
        Assert.True(consumed > 500, $"only {consumed} presses consumed");
    }

    /// <summary>The worker's half: run the machine on every input the gate posted, check the gate agreed, publish the state back.</summary>
    private static void Drain(ChannelReader<WorkerMessage> reader, InputGate gate, CaptureStateMachine machine, int sequence)
    {
        while (reader.TryRead(out var message))
        {
            if (message.Kind != WorkerMessage.MessageKind.Input)
            {
                continue;
            }

            var outcomes = machine.Handle(message.Event!);
            if (message.Event is CaptureEvent.ButtonDown or CaptureEvent.ButtonUp or CaptureEvent.Wheel)
            {
                var machineSays = outcomes.Any(outcome => outcome is CaptureOutcome.Suppress);
                Assert.True(machineSays == message.HookSuppressed, $"sequence {sequence}: {message.Event}: hook {message.HookSuppressed}, machine {machineSays}");
            }

            gate.PublishState(machine.State);
        }
    }

    /// <summary>Plausible input: each button alternates down and up, moves and wheel ticks between, Control held on some presses; every button released at the end.</summary>
    private static List<RawInput> Generate(Random rng)
    {
        var events = new List<RawInput>();
        var isDown = new bool[Buttons.Length];
        long t = 0;
        int x = rng.Next(0, 1000), y = rng.Next(0, 1000);
        var count = rng.Next(5, 80);

        for (var i = 0; i < count; i++)
        {
            t += rng.Next(0, 300);
            var button = Buttons[rng.Next(Buttons.Length)];
            switch (rng.Next(6))
            {
                case 0 or 1 when !isDown[(int)button]:
                    isDown[(int)button] = true;
                    events.Add(RawInput.ButtonDown(button, x, y, t, rng.Next(10) == 0 ? KeyModifiers.Control : KeyModifiers.None));
                    break;
                case 2 when isDown[(int)button]:
                    isDown[(int)button] = false;
                    events.Add(RawInput.ButtonUp(button, x, y, t));
                    break;
                case 3 or 4:
                    x += rng.Next(-40, 41);
                    y += rng.Next(-40, 41);
                    events.Add(RawInput.Move(x, y, t));
                    break;
                default:
                    events.Add(RawInput.WheelTick(rng.Next(2) == 0 ? WheelDirection.Up : WheelDirection.Down, x, y, t));
                    break;
            }
        }

        foreach (var button in Buttons.Where(b => isDown[(int)b]))
        {
            events.Add(RawInput.ButtonUp(button, x, y, ++t));
        }

        return events;
    }
}
