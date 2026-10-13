using Augram.Core.Abstractions;
using Augram.Core.Steps.Hotkey;
using Augram.Core.Tests.Platforms;
using Xunit;

namespace Augram.Core.Tests.Steps;

/// <summary>
/// <see cref="HotkeyText.Layouts"/> is process-wide like <see cref="RunningPlatform"/>, so this runs in the same never-parallel
/// collection: every other test sees the null object and reads US names.
/// </summary>
[Collection(RunningPlatformTests.Alone)]
public sealed class HotkeyTextLayoutsHookTests
{
    [Fact]
    public void Unset_KeyNamesAreTheUsOnes()
    {
        Assert.Same(NullKeyboardLayouts.Instance, HotkeyText.Layouts);
        Assert.Equal(";", HotkeyText.KeyName(KeyCode.Semicolon));
    }

    [Fact]
    public void Set_EveryNameWithoutAnExplicitPortFollowsIt_UntilReset()
    {
        var swedish = new FakeKeyboardLayouts(FakeKeyboardLayouts.Swedish) { Characters = { [KeyCode.Semicolon] = "ö" } };
        try
        {
            HotkeyText.Layouts = swedish;

            Assert.Equal("Ö", HotkeyText.KeyName(KeyCode.Semicolon));
            Assert.Equal("Ctrl+Ö", HotkeyText.Format(KeyModifiers.Control, KeyCode.Semicolon));
            Assert.Equal("Ctrl+Ö", new HotkeyStep(KeyModifiers.Control, KeyCode.Semicolon).Summary);
            Assert.Equal(";", HotkeyText.KeyName(KeyCode.Semicolon, layouts: NullKeyboardLayouts.Instance));
        }
        finally
        {
            HotkeyText.Layouts = NullKeyboardLayouts.Instance;
        }

        Assert.Equal(";", HotkeyText.KeyName(KeyCode.Semicolon));
    }
}
