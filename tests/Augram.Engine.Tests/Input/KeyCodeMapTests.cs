using Augram.Core.Abstractions;
using Augram.Core.Capture;
using Augram.Engine.Input;
using Xunit;

namespace Augram.Engine.Tests.Input;

/// <summary>Every Core key and button has a SharpHook counterpart and round-trips; the US layout table is consistent.</summary>
public sealed class KeyCodeMapTests
{
    [Fact]
    public void EveryCoreKey_MapsToASharpHookKey()
    {
        Assert.Empty(KeyCodeMap.Unmapped());
    }

    [Fact]
    public void Keys_RoundTrip()
    {
        foreach (var key in Enum.GetValues<KeyCode>().Where(k => k != KeyCode.None))
        {
            var hook = KeyCodeMap.ToHook(key);
            Assert.NotEqual(SharpHook.Data.KeyCode.VcUndefined, hook);
            Assert.Equal(key, KeyCodeMap.ToCore(hook));
        }

        Assert.Equal(KeyCode.None, KeyCodeMap.ToCore(SharpHook.Data.KeyCode.VcUndefined));
        Assert.Equal(KeyCode.None, KeyCodeMap.ToCore(SharpHook.Data.KeyCode.VcKana));
        Assert.Equal(SharpHook.Data.KeyCode.Vc0, KeyCodeMap.ToHook(KeyCode.Digit0));
        Assert.Equal(SharpHook.Data.KeyCode.VcLeftMeta, KeyCodeMap.ToHook(KeyCode.LeftMeta));
    }

    [Fact]
    public void Buttons_RoundTrip_AndNoButtonIsRejected()
    {
        foreach (var button in Enum.GetValues<MouseButton>())
        {
            Assert.True(MouseButtonMap.TryToCore(MouseButtonMap.ToHook(button), out var back));
            Assert.Equal(button, back);
        }

        Assert.Equal(SharpHook.Data.MouseButton.Button1, MouseButtonMap.ToHook(MouseButton.Left));
        Assert.Equal(SharpHook.Data.MouseButton.Button2, MouseButtonMap.ToHook(MouseButton.Right));
        Assert.Equal(SharpHook.Data.MouseButton.Button3, MouseButtonMap.ToHook(MouseButton.Middle));
        Assert.False(MouseButtonMap.TryToCore(SharpHook.Data.MouseButton.NoButton, out _));
    }

    [Theory]
    [InlineData('a', KeyCode.A, false)]
    [InlineData('Z', KeyCode.Z, true)]
    [InlineData('7', KeyCode.Digit7, false)]
    [InlineData('&', KeyCode.Digit7, true)]
    [InlineData(' ', KeyCode.Space, false)]
    [InlineData('\n', KeyCode.Enter, false)]
    [InlineData('?', KeyCode.Slash, true)]
    [InlineData('`', KeyCode.BackQuote, false)]
    public void AsciiLayout_MapsPrintableCharacters(char c, KeyCode expected, bool shift)
    {
        Assert.True(AsciiKeyLayout.TryGetKey(c, out var key, out var isShift));
        Assert.Equal(expected, key);
        Assert.Equal(shift, isShift);
    }

    [Theory]
    [InlineData('é')]
    [InlineData('\u0001')]
    public void AsciiLayout_RejectsWhatItCannotType(char c)
    {
        Assert.False(AsciiKeyLayout.TryGetKey(c, out var key, out _));
        Assert.Equal(KeyCode.None, key);
    }
}
