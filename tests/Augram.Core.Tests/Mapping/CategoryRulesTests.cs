using Augram.Core.Mapping;
using Xunit;
using static Augram.Core.Tests.Mapping.Support.MappingFixtures;

namespace Augram.Core.Tests.Mapping;

public sealed class CategoryRulesTests
{
    [Fact]
    public void CategoriesAreTrimmedAndSortedByNameIgnoringCase()
    {
        var global = NewGlobal() with { Categories = [NewCategory(" window "), NewCategory("Media"), NewCategory("clipboard")] };

        var valid = MappingRules.ValidDocument(Document(global));

        Assert.Equal(["clipboard", "Media", "window"], valid.Global.Categories.Select(category => category.Name));
    }

    [Theory]
    [InlineData("Media")]
    [InlineData("media")]
    [InlineData(" MEDIA ")]
    public void CategoryNamesAreUniqueWithinAGroupIgnoringCase(string duplicate)
    {
        var global = NewGlobal() with { Categories = [NewCategory("Media"), NewCategory(duplicate)] };

        var ex = Assert.Throws<MappingValidationException>(() => MappingRules.ValidDocument(Document(global)));

        Assert.Equal("A category named 'Media' already exists in 'Global'.", ex.Message);
    }

    [Fact]
    public void TheSameCategoryNameAndIdMayRepeatAcrossGroups()
    {
        var media = NewCategory("Media");

        var valid = MappingRules.ValidDocument(Document(
            NewGlobal() with { Categories = [media] },
            NewGroup("Chrome") with { Categories = [media] }));

        Assert.Equal(media, Assert.Single(valid.Groups[1].Categories));
    }

    [Fact]
    public void AnEmptyCategoryNameOrARepeatedIdIsRejected()
    {
        var empty = Assert.Throws<MappingValidationException>(() => MappingRules.ValidDocument(
            Document(NewGlobal() with { Categories = [NewCategory("  ")] })));
        Assert.Equal("A category in 'Global' needs a name.", empty.Message);

        var media = NewCategory("Media");
        var twice = Assert.Throws<MappingValidationException>(() => MappingRules.ValidDocument(
            Document(NewGlobal(), NewGroup("Chrome") with { Categories = [media, media with { Name = "Audio" }] })));
        Assert.Equal($"A category with id {media.Id} already exists in 'Chrome'.", twice.Message);
    }

    [Fact]
    public void ACommandInACategoryItsGroupLacksBecomesUncategorized()
    {
        var media = NewCategory("Media");
        var elsewhere = NewCategory("Elsewhere");
        var global = NewGlobal(NewCommand("Play").In(media), NewCommand("Stray").In(elsewhere), NewCommand("Loose")) with { Categories = [media] };

        var valid = MappingRules.ValidDocument(Document(global));

        Assert.Equal([null, media.Id, null], valid.Global.Commands.Select(command => command.CategoryId));
    }

    [Fact]
    public void FindByNameIgnoresCaseAndSurroundingSpace()
    {
        var media = NewCategory("Media");
        var global = NewGlobal() with { Categories = [media] };

        Assert.Same(media, CategoryRules.FindByName(global, " MEDIA "));
        Assert.Null(CategoryRules.FindByName(global, "Audio"));
    }

    [Fact]
    public void CarriedFindsTheTargetCategoryByNameOrNone()
    {
        var media = NewCategory("Media");
        var window = NewCategory("Window");
        var theirMedia = NewCategory("media");
        var from = NewGlobal() with { Categories = [media, window] };
        var to = NewGroup("Chrome") with { Categories = [theirMedia] };

        Assert.Equal(theirMedia.Id, CategoryRules.Carried(media.Id, from, to));
        Assert.Null(CategoryRules.Carried(window.Id, from, to));
        Assert.Null(CategoryRules.Carried(null, from, to));
        Assert.Null(CategoryRules.Carried(CategoryId.New(), from, to));
    }
}
