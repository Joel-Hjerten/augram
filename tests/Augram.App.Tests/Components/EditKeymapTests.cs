using Augram.App.Components.GestureGrid;
using Augram.App.Components.ListEditing;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Xunit;

namespace Augram.App.Tests.Components;

public sealed class EditKeymapTests
{
    [Fact]
    public void WindowsUsesCtrlF2OrEnterAndCtrlY()
    {
        var keymap = new EditKeymap(isMacOS: false);

        Assert.Equal(["Ctrl+N", "Ctrl+C", "Ctrl+V", "Ctrl+D", "Delete", "F2", "Enter", "Ctrl+Z", "Ctrl+Y"], keymap.All);
        Assert.All(keymap.All, gesture => Assert.NotNull(KeyGesture.Parse(gesture)));
    }

    [Fact]
    public void MacOSUsesCmdReturnAndCmdShiftZ()
    {
        var keymap = new EditKeymap(isMacOS: true);

        Assert.Equal(["Cmd+N", "Cmd+C", "Cmd+V", "Cmd+D", "Delete", "Return", "Cmd+Z", "Cmd+Shift+Z"], keymap.All);
        Assert.All(keymap.All, gesture => Assert.NotNull(KeyGesture.Parse(gesture)));
    }

    [Fact]
    public void CurrentMatchesThisMachine()
    {
        Assert.Equal(OperatingSystem.IsMacOS(), EditKeymap.Current.IsMacOS);
    }

    /// <summary>Q11 (Joel, 2026-10-11): the Gestures tab renames on F2 and, as the Commands tab does, on Enter on Windows; on Return on macOS.</summary>
    [AvaloniaFact]
    public void TheGestureGridRenamesOnTheCommandsTabsKeys_EnterIncludedOnWindows()
    {
        Assert.Equal([KeyGesture.Parse("F2"), KeyGesture.Parse("Enter")], RenameKeys(new EditKeymap(isMacOS: false)));
        Assert.Equal([KeyGesture.Parse("Return")], RenameKeys(new EditKeymap(isMacOS: true)));
    }

    private static List<KeyGesture> RenameKeys(EditKeymap keymap)
    {
        var target = new Border();
        var requested = new List<GestureGridAction>();
        GestureGridMenu.BindKeys(target, keymap, requested.Add, () => true);
        var renames = new List<KeyGesture>();
        foreach (var binding in target.KeyBindings)
        {
            requested.Clear();
            binding.Command!.Execute(null);
            if (requested is [GestureGridAction.Rename])
            {
                renames.Add(binding.Gesture!);
            }
        }

        return renames;
    }
}
