using Augram.Core.Abstractions;
using Augram.Platform.MacOS.Input;
using Xunit;

namespace Augram.Platform.MacOS.Tests.Input;

/// <summary>Core keys → macOS virtual keys (<c>kVK_ANSI_*</c>) for the keys a layout names by character (plan 0011).</summary>
public sealed class MacVirtualKeysTests
{
    [Theory]
    [InlineData(KeyCode.A, 0x00)]
    [InlineData(KeyCode.Z, 0x06)]
    [InlineData(KeyCode.Digit1, 0x12)]
    [InlineData(KeyCode.Digit5, 0x17)]
    [InlineData(KeyCode.Digit0, 0x1D)]
    [InlineData(KeyCode.Semicolon, 0x29)]
    [InlineData(KeyCode.OpenBracket, 0x21)]
    [InlineData(KeyCode.CloseBracket, 0x1E)]
    [InlineData(KeyCode.BackQuote, 0x32)]
    public void AKeyMapsToItsAnsiVirtualKey(KeyCode key, int virtualKey)
    {
        Assert.True(MacVirtualKeys.TryMap(key, out var mapped));
        Assert.Equal(virtualKey, mapped);
    }

    [Fact]
    public void EveryLetterDigitAndPunctuationKeyMaps_ToADistinctVirtualKey()
    {
        var keys = Enum.GetValues<KeyCode>()
            .Where(key => key is (>= KeyCode.A and <= KeyCode.Z) or (>= KeyCode.Digit0 and <= KeyCode.Digit9) or (>= KeyCode.BackQuote and <= KeyCode.Slash))
            .ToList();

        var mapped = keys.Select(key => (Ok: MacVirtualKeys.TryMap(key, out var virtualKey), VirtualKey: virtualKey)).ToList();

        Assert.Equal(47, keys.Count);
        Assert.All(mapped, pair => Assert.True(pair.Ok));
        Assert.Equal(keys.Count, mapped.Select(pair => pair.VirtualKey).Distinct().Count());
    }

    [Theory]
    [InlineData(KeyCode.None)]
    [InlineData(KeyCode.F1)]
    [InlineData(KeyCode.Escape)]
    [InlineData(KeyCode.Space)]
    [InlineData(KeyCode.NumPad4)]
    [InlineData(KeyCode.VolumeUp)]
    public void AKeyWithANameOfItsOwnIsNotTranslated(KeyCode key) => Assert.False(MacVirtualKeys.TryMap(key, out _));

    [Theory]
    [InlineData(KeyCode.Space, 0x31)]
    [InlineData(KeyCode.A, 0x00)]
    [InlineData(KeyCode.F1, 0x7A)]
    [InlineData(KeyCode.Escape, 0x35)]
    [InlineData(KeyCode.NumPad4, 0x56)]
    [InlineData(KeyCode.Up, 0x7E)]
    public void AHeldKeyIsPostedAsItsVirtualKey(KeyCode key, int virtualKey)
    {
        Assert.True(MacVirtualKeys.TryMapForPosting(key, out var mapped));
        Assert.Equal(virtualKey, mapped);
    }

    [Theory]
    [InlineData(KeyCode.None)]
    [InlineData(KeyCode.LeftShift)]
    [InlineData(KeyCode.VolumeUp)]
    [InlineData(KeyCode.F24)]
    public void AModifierMediaOrMissingKeyHasNoVirtualKeyToPost(KeyCode key) => Assert.False(MacVirtualKeys.TryMapForPosting(key, out _));
}
