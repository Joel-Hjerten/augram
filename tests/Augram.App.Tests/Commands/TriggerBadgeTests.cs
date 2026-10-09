using Augram.App.Components.CommandTree;
using Augram.Core.Abstractions;
using Augram.Core.Capture;
using Augram.Core.Gestures;
using Augram.Core.Mapping;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Xunit;

namespace Augram.App.Tests.Commands;

/// <summary>
/// A command row's trigger badge (no glyph to draw) shows one word per line and shrinks rather than overflow its tile:
/// "Wheel up" used to read "Whee / l up" and "No trigger" was cut to "No trigge" (Joel, 2026-10-09). A combination keeps
/// each "+" with the word before it ("Right +" / "wheel" / "up").
/// </summary>
public sealed class TriggerBadgeTests
{
    public static TheoryData<string, string> Triggers => new()
    {
        { "wheel up", "Wheel\nup" },
        { "wheel down", "Wheel\ndown" },
        { "none", "No\ntrigger" },
        { "missing gesture", "Missing\ngesture" },
        { "right wheel up", "Right +\nwheel\nup" },
        { "ctrl shift click", "Ctrl +\nShift +\nclick" },
    };

    [AvaloniaTheory]
    [MemberData(nameof(Triggers))]
    public void TheBadgeShowsOneWordPerLineInsideItsTile(string trigger, string expected)
    {
        var row = new CommandRow { Item = CommandItem.From(AppGroup.EmptyGlobal, Command(trigger), null, HostPlatform.Windows) };
        var window = new Window { Content = row, Width = 400, Height = 120 };
        window.Show();
        Dispatcher.UIThread.RunJobs();

        var text = row.GetVisualDescendants().OfType<TextBlock>().Single(block => block.Classes.Contains("trigger-badge"));
        var tile = row.GetVisualDescendants().OfType<Border>().Single(border => border.Classes.Contains("trigger-badge"));
        var corner = text.TranslatePoint(new Point(text.Bounds.Width, text.Bounds.Height), tile)!.Value;
        var shown = text.Text;
        window.Close();

        Assert.Equal(expected, shown);
        Assert.True(corner.X <= tile.Bounds.Width + 0.5 && corner.Y <= tile.Bounds.Height + 0.5, $"the text reaches {corner} in a {tile.Bounds.Size} tile");
    }

    private static Command Command(string trigger) => new(
        CommandId.New(),
        "Synthetic",
        trigger switch
        {
            "wheel up" => Trigger.ForWheel(WheelDirection.Up),
            "wheel down" => Trigger.ForWheel(WheelDirection.Down),
            "missing gesture" => Trigger.ForGesture(GestureId.New()),
            "right wheel up" => Trigger.ForWheel(WheelDirection.Up, new TriggerHold(HeldButtons.Right)),
            "ctrl shift click" => Trigger.ForClick(TriggerHold.WithStroke(KeyModifiers.Control | KeyModifiers.Shift)),
            _ => Trigger.None,
        },
        IsActive: true,
        []);
}
