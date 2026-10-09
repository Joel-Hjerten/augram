using Augram.Core.Abstractions;
using Augram.Core.Capture;
using Augram.Core.Steps.Hotkey;

namespace Augram.Core.HoldRemaps;

/// <summary>
/// What fires a command under a hold remap while its hold key is held (F9): a mouse button or a set of buttons held
/// together (matched exactly, plan 0002 decision 7), a wheel direction, or a key. A closed set of records with value
/// equality, like <c>Mapping.Trigger</c>, whose <c>InputTrigger</c> carries one. The constructor is private: the three
/// nested records are the whole set.
/// </summary>
public abstract record HoldInput
{
    private HoldInput()
    {
    }

    /// <summary>"Left + Right", "wheel up", "W": the input alone, for a command row and the log.</summary>
    public abstract string Describe();

    /// <summary>The input in its stored form: a button set cut down to the physical buttons.</summary>
    public virtual HoldInput Normalised() => this;

    /// <summary>One button: <c>Of(MouseButton.Left)</c>.</summary>
    public static Buttons Of(MouseButton button) => new(button.Flag());

    /// <summary>Buttons held together: <c>Of(MouseButton.Left, MouseButton.Right)</c> zooms where Left orbits and Right pans.</summary>
    public static Buttons Of(params MouseButton[] buttons)
    {
        ArgumentNullException.ThrowIfNull(buttons);
        var set = HeldButtons.None;
        foreach (var button in buttons)
        {
            set |= button.Flag();
        }

        return new(set);
    }

    /// <summary>
    /// The physical buttons in <paramref name="Set"/>, all held at once and nothing else (<see cref="HeldButtons.Stroke"/>
    /// means nothing here: an input names physical buttons, and the stroke button is one of them like any other).
    /// </summary>
    public sealed record Buttons(HeldButtons Set) : HoldInput
    {
        public override string Describe() => string.Join(" + ", Set.Buttons().Select(button => button.ToString()));

        public override HoldInput Normalised() => (Set & ~HeldButtonsExtensions.Physical) == HeldButtons.None ? this : new Buttons(Set & HeldButtonsExtensions.Physical);
    }

    /// <summary>One wheel notch in <paramref name="Direction"/>; every notch fires.</summary>
    public sealed record Wheel(WheelDirection Direction) : HoldInput
    {
        public override string Describe() => Direction == WheelDirection.Up ? "wheel up" : "wheel down";
    }

    /// <summary>A key; never a modifier and never the hold key itself (<see cref="HoldRemapRules"/>).</summary>
    public sealed record Key(KeyCode KeyCode) : HoldInput
    {
        public override string Describe() => HotkeyText.KeyName(KeyCode);
    }
}
