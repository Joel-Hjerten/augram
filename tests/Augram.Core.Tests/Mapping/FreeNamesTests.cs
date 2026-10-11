using Augram.Core.Gestures;
using Augram.Core.Mapping;
using Xunit;

namespace Augram.Core.Tests.Mapping;

/// <summary>A fresh item's name: "New command 1", the smallest number free, compared with the caller's comparer; not the "(2)" rule of <see cref="NameScope"/>.</summary>
public sealed class FreeNamesTests
{
    [Fact]
    public void TheSmallestFreeNumberIsTaken()
    {
        Assert.Equal("New command 1", FreeNames.Next("New command", [], MappingRules.NameComparer));
        Assert.Equal("New command 2", FreeNames.Next("New command", ["new command 1", "New command 3"], MappingRules.NameComparer));
        Assert.Equal("New gesture 1", FreeNames.Next("New gesture", ["New gesture"], GestureRules.NameComparer));
    }

    [Fact]
    public void TheStemAloneComesFirstWhenAsked()
    {
        Assert.Equal("Hold remap", FreeNames.Next("Hold remap", ["Space"], MappingRules.NameComparer, stemFirst: true));
        Assert.Equal("Hold remap 1", FreeNames.Next("Hold remap", ["Hold remap"], MappingRules.NameComparer, stemFirst: true));
    }

    [Theory]
    [InlineData("Hold remap", true)]
    [InlineData("hold remap 12", true)]
    [InlineData("Hold remap 2b", false)]
    [InlineData("Hold remap ", false)]
    [InlineData("Space", false)]
    public void ANumberedNameIsRecognised(string name, bool numbered)
    {
        Assert.Equal(numbered, FreeNames.IsNumbered("Hold remap", name, MappingRules.NameComparer));
    }
}
