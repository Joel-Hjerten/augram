using System.Text.Json;
using Augram.Core.Abstractions;
using Augram.Core.Capture;
using Augram.Core.Gestures;
using Augram.Core.Mapping;
using Augram.Core.Steps.Imported;

namespace Augram.Import.StrokesPlus;

/// <summary>
/// An SP.net action's trigger as an Augram <see cref="Trigger"/> (learnings 0003 §3.8, Joel 2026-10-09: real triggers, not
/// "inactive with a note"): the gesture by name, else exactly one wheel flag, else a click when keys or buttons are held, else
/// none; <c>Control</c>/<c>Alt</c>/<c>Shift</c> and the <c>Left</c>…<c>X2</c> flags are the "while holding" set with the
/// action's <c>Capture</c> (0 Before, 1 After, 2 Either). A <c>UseSecondaryStrokeButton</c> wheel action holds the file's
/// secondary stroke button instead of the stroke button ("Right + wheel up"); with none set there, it holds Right (Joel's
/// "Right + wheel = zoom") and imports inactive with a note saying to check the button. Two cases import inactive with a note
/// because Augram does them differently: a gesture or click drawn with the secondary button (Augram draws with the stroke
/// button only), and a click trigger whose steps only re-send a mouse click (SP.net's "Shift+Right Click": Augram passes
/// such a click through itself when nothing is bound).
/// </summary>
internal sealed class TriggerReader
{
    public const string SecondaryNote = "Imported from StrokesPlus.net: a secondary-stroke-button command, and no secondary button was set there; imported as Right + wheel and inactive: check the button to hold, then switch it on.";
    public const string SecondaryStrokeNote = "Imported from StrokesPlus.net, inactive: it was drawn or clicked with the secondary stroke button, and Augram draws and clicks with the stroke button only.";
    public const string RelayNote = "Imported from StrokesPlus.net, inactive: it re-sent the click, and Augram passes a click with keys held through to the app itself when nothing is bound to it.";

    private readonly List<ImportWarning> _warnings;
    private readonly IReadOnlyDictionary<string, GestureId> _gestures;
    private readonly MouseButton? _secondary;

    public TriggerReader(List<ImportWarning> warnings, IReadOnlyDictionary<string, GestureId> gesturesByName, MouseButton? secondary)
    {
        _warnings = warnings;
        _gestures = gesturesByName;
        _secondary = secondary;
    }

    /// <summary>The file's secondary stroke button (a WinForms <c>MouseButtons</c> value), or null for none or one Augram does not know.</summary>
    public static MouseButton? SecondaryOf(JsonElement root) => JsonRead.Integer(root, StrokesPlusJson.SecondaryStrokeButton) switch
    {
        StrokesPlusJson.MouseButtons.Left => MouseButton.Left,
        StrokesPlusJson.MouseButtons.Right => MouseButton.Right,
        StrokesPlusJson.MouseButtons.Middle => MouseButton.Middle,
        StrokesPlusJson.MouseButtons.X1 => MouseButton.X1,
        StrokesPlusJson.MouseButtons.X2 => MouseButton.X2,
        _ => null,
    };

    /// <summary>The trigger, and the note when the command must import inactive (null when it keeps the action's active flag).</summary>
    public (Trigger Trigger, string? InactiveNote) Read(JsonElement action, string commandName, IReadOnlyList<CommandStep> steps)
    {
        var keys = Keys(action);
        var buttons = Buttons(action);
        var capture = CaptureOf(action);
        var secondary = JsonRead.Flag(action, StrokesPlusJson.Action.UseSecondaryStrokeButton);
        var withStroke = new TriggerHold(HeldButtons.Stroke | buttons, keys, capture);

        var gestureName = JsonRead.Text(action, StrokesPlusJson.Action.GestureName);
        if (gestureName.Length > 0)
        {
            if (!_gestures.TryGetValue(gestureName, out var id))
            {
                _warnings.Add(new ImportWarning(ImportSeverity.Warning, commandName, $"gesture '{gestureName}' not found; command imported without a gesture"));
                return (Trigger.None, secondary ? SecondaryStrokeNote : null);
            }

            return (Trigger.ForGesture(id, withStroke), secondary ? SecondaryStrokeNote : null);
        }

        var up = JsonRead.Flag(action, StrokesPlusJson.Action.WheelUp);
        var down = JsonRead.Flag(action, StrokesPlusJson.Action.WheelDown);
        if (up && down)
        {
            _warnings.Add(new ImportWarning(ImportSeverity.Warning, commandName, "Both wheel directions are set; imported as wheel up."));
        }

        if (up || down)
        {
            var direction = up ? WheelDirection.Up : WheelDirection.Down;
            if (!secondary)
            {
                return (Trigger.ForWheel(direction, withStroke), null);
            }

            var anchor = _secondary ?? MouseButton.Right;
            return (Trigger.ForWheel(direction, new TriggerHold(anchor.Flag() | buttons, keys, capture)), _secondary is null ? SecondaryNote : null);
        }

        if (keys != KeyModifiers.None || buttons != HeldButtons.None)
        {
            var click = Trigger.ForClick(withStroke);
            var note = secondary ? SecondaryStrokeNote : ResendsTheClick(steps) ? RelayNote : null;
            return (click, note);
        }

        _warnings.Add(new ImportWarning(ImportSeverity.Warning, commandName, "No gesture or wheel trigger; command imported without a trigger."));
        return (Trigger.None, null);
    }

    private static KeyModifiers Keys(JsonElement action)
        => (JsonRead.Flag(action, StrokesPlusJson.Action.Control) ? KeyModifiers.Control : KeyModifiers.None)
            | (JsonRead.Flag(action, StrokesPlusJson.Action.Alt) ? KeyModifiers.Alt : KeyModifiers.None)
            | (JsonRead.Flag(action, StrokesPlusJson.Action.Shift) ? KeyModifiers.Shift : KeyModifiers.None);

    private static HeldButtons Buttons(JsonElement action)
        => (JsonRead.Flag(action, StrokesPlusJson.Action.Left) ? HeldButtons.Left : HeldButtons.None)
            | (JsonRead.Flag(action, StrokesPlusJson.Action.Middle) ? HeldButtons.Middle : HeldButtons.None)
            | (JsonRead.Flag(action, StrokesPlusJson.Action.Right) ? HeldButtons.Right : HeldButtons.None)
            | (JsonRead.Flag(action, StrokesPlusJson.Action.X1) ? HeldButtons.X1 : HeldButtons.None)
            | (JsonRead.Flag(action, StrokesPlusJson.Action.X2) ? HeldButtons.X2 : HeldButtons.None);

    /// <summary>SP.net's order (0 Before, 1 After, 2 Either), not classic's; anything else, or missing, is Either.</summary>
    private static HoldCapture CaptureOf(JsonElement action) => JsonRead.Integer(action, StrokesPlusJson.Action.Capture) switch
    {
        0 => HoldCapture.Before,
        1 => HoldCapture.After,
        _ => HoldCapture.Either,
    };

    /// <summary>The steps re-send a mouse click (SP.net's "Shift+Right Click": ConsumePhysicalInput, MouseClick, ConsumePhysicalInput).</summary>
    private static bool ResendsTheClick(IReadOnlyList<CommandStep> steps)
        => steps.Any(step => step.Step is ImportedStep { SourceMethod: StrokesPlusJson.Method.MouseClick });
}
