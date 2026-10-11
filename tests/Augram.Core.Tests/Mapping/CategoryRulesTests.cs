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
    public void ACommandInACategoryItsAppGroupLacksHasNone()
    {
        var media = NewCategory("Media");
        var elsewhere = NewCategory("Elsewhere");
        var chrome = NewGroup("Chrome", null, NewCommand("Play").In(media), NewCommand("Stray").In(elsewhere), NewCommand("Loose")) with { Categories = [media] };

        var valid = MappingRules.ValidDocument(Document(NewGlobal(), chrome));

        Assert.Equal([null, media.Id, null], valid.Groups[1].Commands.Select(command => command.CategoryId));
        Assert.Equal([media], valid.Groups[1].Categories);
    }

    [Fact]
    public void OnGlobal_ACommandWithoutACategory_OrInOneTheGroupLacks_GoesIntoUncategorized_MadeOnce()
    {
        var media = NewCategory("Media");
        var elsewhere = NewCategory("Elsewhere");
        var global = NewGlobal(NewCommand("Play").In(media), NewCommand("Stray").In(elsewhere), NewCommand("Loose")) with { Categories = [media] };

        var valid = MappingRules.ValidDocument(Document(global));

        Assert.Equal([Uncategorized.Id, media.Id, Uncategorized.Id], valid.Global.Commands.Select(command => command.CategoryId));
        Assert.Equal([media, Uncategorized], valid.Global.Categories);
        Assert.Equal(PlatformSet.All, valid.Global.Categories[1].UseOn);
        Assert.Same(valid.Global.Categories[1], CategoryRules.Uncategorized(valid.Global));
    }

    [Fact]
    public void AGlobalWhoseCommandsAllHaveACategory_GetsNoUncategorized()
    {
        var media = NewCategory("Media");

        var valid = MappingRules.ValidDocument(Document(NewGlobal(NewCommand("Play").In(media)) with { Categories = [media] }));

        Assert.Equal([media], valid.Global.Categories);
        Assert.Null(CategoryRules.Uncategorized(valid.Global));
    }

    [Fact]
    public void ACategoryNamedUncategorized_IsTheOneUsed_WhateverItsIdOrCase()
    {
        var own = NewCategory(" uncategorized ") with { UseOn = PlatformSet.Windows };
        var global = NewGlobal(NewCommand("Loose")) with { Categories = [own] };

        var valid = MappingRules.ValidDocument(Document(global));

        var category = Assert.Single(valid.Global.Categories);
        Assert.Equal((own.Id, "uncategorized", PlatformSet.Windows), (category.Id, category.Name, category.UseOn));
        Assert.Equal(own.Id, valid.Global.Commands[0].CategoryId);
    }

    [Fact]
    public void ARenamedUncategorizedStaysRenamed_AndANewCommandWithoutOneMakesAnotherUncategorized_WithTheNextFixedId()
    {
        var misc = Uncategorized with { Name = "Misc" };
        var global = NewGlobal(NewCommand("Play").In(misc), NewCommand("Loose")) with { Categories = [misc] };

        var valid = MappingRules.ValidDocument(Document(global));
        var again = MappingRules.ValidDocument(Document(global));

        Assert.Equal(["Misc", "Uncategorized"], valid.Global.Categories.Select(category => category.Name));
        var fresh = valid.Global.Categories[1];
        Assert.NotEqual(misc.Id, fresh.Id);
        Assert.Equal([fresh.Id, misc.Id], valid.Global.Commands.Select(command => command.CategoryId));
        Assert.Equal(fresh.Id, again.Global.Categories[1].Id);
    }

    [Fact]
    public void NormalisingTwiceChangesNothing()
    {
        var global = NewGlobal(NewCommand("Loose"), NewCommand("Play").In(NewCategory("Gone")));

        var once = MappingRules.ValidDocument(Document(global));
        var twice = MappingRules.ValidDocument(once);

        Assert.Equal(once.Global.Categories, twice.Global.Categories);
        Assert.Equal(once.Global.Commands, twice.Global.Commands);
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
