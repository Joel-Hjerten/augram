using Augram.App.Components.CommandTree;
using Avalonia.Input;
using Xunit;

namespace Augram.App.Tests.Commands;

public sealed class CommandsKeymapTests
{
    [Fact]
    public void WindowsUsesCtrlF2OrEnterAndCtrlY()
    {
        var keymap = new CommandsKeymap(isMacOS: false);

        Assert.Equal(["Ctrl+N", "Ctrl+C", "Ctrl+V", "Ctrl+D", "Delete", "F2", "Enter", "Ctrl+Z", "Ctrl+Y"], keymap.All);
        Assert.All(keymap.All, gesture => Assert.NotNull(KeyGesture.Parse(gesture)));
    }

    [Fact]
    public void MacOSUsesCmdReturnAndCmdShiftZ()
    {
        var keymap = new CommandsKeymap(isMacOS: true);

        Assert.Equal(["Cmd+N", "Cmd+C", "Cmd+V", "Cmd+D", "Delete", "Return", "Cmd+Z", "Cmd+Shift+Z"], keymap.All);
        Assert.All(keymap.All, gesture => Assert.NotNull(KeyGesture.Parse(gesture)));
    }

    [Fact]
    public void CurrentMatchesThisMachine()
    {
        Assert.Equal(OperatingSystem.IsMacOS(), CommandsKeymap.Current.IsMacOS);
    }
}
