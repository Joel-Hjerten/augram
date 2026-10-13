using Augram.Core.Abstractions;
using Augram.Platform.MacOS.Input;
using Xunit;

namespace Augram.Platform.MacOS.Tests.Input;

/// <summary>What Text Input Source Services' property values become (plan 0011); the calls themselves run only on a Mac and are checked by Joel there.</summary>
public sealed class MacInputSourcesTests
{
    [Fact]
    public void ASourceIsALayout_ByIdAndLocalizedName()
        => Assert.Equal(new KeyboardLayout("com.apple.keylayout.Swedish-Pro", "Swedish"), MacInputSources.Layout("com.apple.keylayout.Swedish-Pro", "Swedish"));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    public void WithoutANameTheIdStandsIn(string? name)
        => Assert.Equal(new KeyboardLayout("com.apple.keylayout.ABC", "com.apple.keylayout.ABC"), MacInputSources.Layout("com.apple.keylayout.ABC", name));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void WithoutAnIdThereIsNoLayout(string? id) => Assert.Null(MacInputSources.Layout(id, "Swedish"));

    [Theory]
    [InlineData("TISTypeKeyboardLayout", true)]
    [InlineData("TISTypeKeyboardInputMode", false)]
    [InlineData("TISTypeKeyboardInputMethodModeEnabled", false)]
    [InlineData("TISTypeCharacterPalette", false)]
    [InlineData(null, false)]
    public void OnlyAKeyboardLayoutTypeCounts(string? type, bool expected) => Assert.Equal(expected, MacInputSources.IsKeyboardLayout(type));

    [Theory]
    [InlineData("ö", "ö")]
    [InlineData("[", "[")]
    [InlineData("", null)]
    [InlineData(" ", null)]
    [InlineData(null, null)]
    public void ATranslatedStringIsACharacterUnlessEmpty(string? translated, string? expected) => Assert.Equal(expected, MacInputSources.Character(translated));
}
