using Augram.App.Components.CommandTree;
using Augram.App.Tests.Support;
using Augram.App.ViewModels.Commands;
using Augram.Core.Mapping;
using Avalonia.Headless.XUnit;
using Xunit;
using static Augram.App.Tests.Commands.CommandsTestData;

namespace Augram.App.Tests.Commands;

/// <summary>
/// The Global tab's view model: the Global group's categories as sections (Joel, 2026-10-07), every Global command in one;
/// Uncategorized is an ordinary category the store makes for a command left without one (Joel, 2026-10-11).
/// </summary>
public sealed class CommandsViewModelGlobalTests
{
    [AvaloniaFact]
    public void SectionsAreTheCategoriesByName_UncategorizedAnOrdinaryOneThatStaysWhenEmptied()
    {
        var (vm, store, _, _) = Create(CommandsScope.Global);

        Assert.Equal(("Global commands", "New category"), (vm.Heading, vm.NewSectionLabel));
        Assert.Equal(["Media", "Uncategorized", "Window"], Names(vm));
        var uncategorized = Section(vm, "Uncategorized");
        Assert.Equal(SectionId.ForCategory(GroupId.Global, CategoryRules.UncategorizedId), uncategorized.Id);
        Assert.All(vm.Sections, section => Assert.True(section is { CanEditDefinition: false, CanToggleActive: false, CanCopy: false, CanExport: true }));
        Assert.Equal(["Three steps"], uncategorized.Commands.Select(command => command.Name));
        Assert.Equal(["Close window", "Minimize"], Section(vm, "Window").Commands.Select(command => command.Name));
        Assert.Equal(SectionId.ForCategory(GroupId.Global, Category(store, "Window").Id), Section(vm, "Window").Id);

        var close = Item(vm, "Close window");
        Assert.Equal(("Close window", "Up", true), (close.StepSummary, close.TriggerText, close.HasGlyph));
        var volume = Item(vm, "Volume up");
        Assert.Equal((TriggerKind.Wheel, "Wheel up", false), (volume.TriggerKind, volume.TriggerText, volume.HasGlyph));
        Assert.Equal("3 steps", Item(vm, "Three steps").StepSummary);
        Assert.All(vm.Sections.SelectMany(section => section.Commands), command =>
        {
            // No empty choice on Global: every command is in a category.
            Assert.Null(command.CategoryLabel);
            Assert.Equal(["Media", "Uncategorized", "Window"], command.Categories.Select(choice => choice.Name));
            Assert.DoesNotContain(command.Categories, choice => choice.Id is null);
        });

        var three = Item(vm, "Three steps");
        vm.Handle(new CommandTreeActionEventArgs(CommandTreeAction.SetCategory, command: three, category: three.Categories.Single(choice => choice.Name == "Media")));

        Assert.Equal(["Media", "Uncategorized", "Window"], Names(vm));
        Assert.Equal(["Three steps", "Volume up"], Section(vm, "Media").Commands.Select(command => command.Name));
        Assert.Equal("no commands", Section(vm, "Uncategorized").CountText);
    }

    [AvaloniaFact]
    public void NewCategoryAddsANumberedSectionSelectsItAndAsksTheTreeToRenameIt()
    {
        var (vm, store, _, _) = Create(CommandsScope.Global);
        var renames = new List<SectionId>();
        vm.SectionRenameRequested += (_, id) => renames.Add(id);

        vm.Handle(new CommandTreeActionEventArgs(CommandTreeAction.NewSection));

        var created = SectionId.ForCategory(GroupId.Global, Category(store, "New category 1").Id);
        Assert.Equal(created, vm.SelectedSectionId);
        Assert.Null(vm.SelectedCommandId);
        Assert.True(Section(vm, "New category 1").IsExpanded);
        Assert.Equal([created], renames);
        Assert.Equal(["Media", "New category 1", "Uncategorized", "Window"], Names(vm));

        vm.Handle(new CommandTreeActionEventArgs(CommandTreeAction.NewSection));
        Assert.Equal(["Media", "New category 1", "New category 2", "Uncategorized", "Window"], store.Global.Categories.Select(category => category.Name).Order(StringComparer.Ordinal));

        vm.Handle(new CommandTreeActionEventArgs(CommandTreeAction.Undo));
        Assert.Equal(["Media", "New category 1", "Uncategorized", "Window"], Names(vm));
    }

    [AvaloniaFact]
    public void RenameCategoryGoesThroughTheStore_UncategorizedIncluded_AndTheNextCommandWithoutOneMakesANewUncategorized()
    {
        var (vm, store, _, _) = Create(CommandsScope.Global);

        vm.Handle(new CommandTreeActionEventArgs(CommandTreeAction.Rename, Section(vm, "Media"), null, "Sound"));

        Assert.Equal(["Sound", "Uncategorized", "Window"], Names(vm));
        Assert.Equal(Category(store, "Sound").Id, Find(store, "Volume up").CategoryId);
        Assert.Equal(["Volume up"], Section(vm, "Sound").Commands.Select(command => command.Name));

        vm.Handle(new CommandTreeActionEventArgs(CommandTreeAction.Rename, Section(vm, "Uncategorized"), null, "Misc"));

        Assert.Equal(["Misc", "Sound", "Window"], Names(vm));
        Assert.Equal(CategoryRules.UncategorizedId, Category(store, "Misc").Id);
        Assert.Equal(["Three steps"], Section(vm, "Misc").Commands.Select(command => command.Name));

        // Pasted with nothing selected, a copy has no category: the store makes a new Uncategorized for it.
        vm.Handle(new CommandTreeActionEventArgs(CommandTreeAction.Copy, Section(vm, "Misc"), Item(vm, "Three steps")));
        vm.Handle(new CommandTreeActionEventArgs(CommandTreeAction.Select));
        vm.Handle(new CommandTreeActionEventArgs(CommandTreeAction.Paste));

        Assert.Equal(["Misc", "Sound", "Uncategorized", "Window"], Names(vm));
        Assert.NotEqual(CategoryRules.UncategorizedId, Category(store, "Uncategorized").Id);
        Assert.Equal(["Three steps (2)"], Section(vm, "Uncategorized").Commands.Select(command => command.Name));
        Assert.Equal(Section(vm, "Uncategorized").Id, vm.SelectedSectionId);
    }

    [AvaloniaFact]
    public void DeleteCategoryAsksThenMovesItsCommandsToUncategorizedInOneUndoStep()
    {
        var confirm = new FakeConfirmPresenter { Answer = false };
        var (vm, store, _, _) = Create(CommandsScope.Global, confirm);

        vm.Handle(new CommandTreeActionEventArgs(CommandTreeAction.Delete, Section(vm, "Window")));

        Assert.Equal(("Delete category", "Delete category 'Window'? Its 2 commands move to Uncategorized.", "Delete"), confirm.Requests[^1]);
        Assert.Equal(3, store.Global.Categories.Count);

        confirm.Answer = true;
        vm.Handle(new CommandTreeActionEventArgs(CommandTreeAction.Delete, Section(vm, "Window")));

        Assert.Equal(["Media", "Uncategorized"], store.Global.Categories.Select(category => category.Name));
        Assert.Equal(CategoryRules.UncategorizedId, Find(store, "Close window").CategoryId);
        Assert.Equal(CategoryRules.UncategorizedId, Find(store, "Minimize").CategoryId);
        Assert.Equal(["Media", "Uncategorized"], Names(vm));
        Assert.Equal(["Close window", "Minimize", "Three steps"], Section(vm, "Uncategorized").Commands.Select(command => command.Name));
        Assert.Equal($"Deleted 'Window'. {CommandsKeymap.Current.Undo} undoes it.", vm.Message);

        vm.Handle(new CommandTreeActionEventArgs(CommandTreeAction.Undo));

        Assert.Equal(["Media", "Uncategorized", "Window"], Names(vm));
        Assert.Equal(["Close window", "Minimize"], Section(vm, "Window").Commands.Select(command => command.Name));

        vm.Handle(new CommandTreeActionEventArgs(CommandTreeAction.Delete, Section(vm, "Media")));
        Assert.Equal("Delete category 'Media'? Its command moves to Uncategorized.", confirm.Requests[^1].Message);
    }

    [AvaloniaFact]
    public void DeleteUncategorizedAsksThenDeletesItWithItsCommandsInOneUndoStep_AndItHasAFormLikeAnyCategory()
    {
        var confirm = new FakeConfirmPresenter { Answer = false };
        var (vm, store, _, _) = Create(CommandsScope.Global, confirm);

        // One command: counted in the question.
        vm.Handle(new CommandTreeActionEventArgs(CommandTreeAction.Delete, Section(vm, "Uncategorized")));

        Assert.Equal(("Delete category", "Delete category 'Uncategorized' and its command?", "Delete"), confirm.Requests[^1]);
        Assert.Contains(store.Global.Commands, command => command.Name == "Three steps");
        Assert.Equal(["Media", "Uncategorized", "Window"], Names(vm));

        // Several (Window's two moved there): counted, and cancelling leaves them all.
        confirm.Answer = true;
        vm.Handle(new CommandTreeActionEventArgs(CommandTreeAction.Delete, Section(vm, "Window")));
        confirm.Answer = false;
        vm.Handle(new CommandTreeActionEventArgs(CommandTreeAction.Delete, Section(vm, "Uncategorized")));

        Assert.Equal(("Delete category", "Delete category 'Uncategorized' and its 3 commands?", "Delete"), confirm.Requests[^1]);
        Assert.Equal(["Close window", "Minimize", "Three steps"], Section(vm, "Uncategorized").Commands.Select(command => command.Name));

        confirm.Answer = true;
        vm.Handle(new CommandTreeActionEventArgs(CommandTreeAction.Select, Section(vm, "Uncategorized")));
        Assert.NotNull(vm.GroupForm);
        vm.Handle(new CommandTreeActionEventArgs(CommandTreeAction.Delete, Section(vm, "Uncategorized")));

        Assert.Equal(["Volume up"], store.Global.Commands.Select(command => command.Name));
        Assert.Equal(["Media"], store.Global.Categories.Select(category => category.Name));
        Assert.Equal(["Media"], Names(vm));
        Assert.Null(vm.SelectedSectionId);
        Assert.Equal($"Deleted 'Uncategorized'. {CommandsKeymap.Current.Undo} undoes it.", vm.Message);

        // One undo brings it back with all three; the next brings Window back.
        vm.Handle(new CommandTreeActionEventArgs(CommandTreeAction.Undo));

        Assert.Equal(["Media", "Uncategorized"], Names(vm));
        Assert.Equal(["Close window", "Minimize", "Three steps"], Section(vm, "Uncategorized").Commands.Select(command => command.Name));

        vm.Handle(new CommandTreeActionEventArgs(CommandTreeAction.Undo));
        Assert.Equal(["Media", "Uncategorized", "Window"], Names(vm));
    }

    [AvaloniaFact]
    public void DeletingACategoryWithoutAnUncategorized_MakesOneForItsCommands()
    {
        var (vm, store, _, _) = Create(CommandsScope.Global, new FakeConfirmPresenter());
        vm.Handle(new CommandTreeActionEventArgs(CommandTreeAction.Delete, Section(vm, "Uncategorized")));
        Assert.Equal(["Media", "Window"], Names(vm));

        vm.Handle(new CommandTreeActionEventArgs(CommandTreeAction.Delete, Section(vm, "Media")));

        Assert.Equal(["Uncategorized", "Window"], Names(vm));
        Assert.Equal(["Volume up"], Section(vm, "Uncategorized").Commands.Select(command => command.Name));
        Assert.Equal(CategoryRules.UncategorizedId, Find(store, "Volume up").CategoryId);
    }

    [AvaloniaFact]
    public void NewCommandLandsInTheSelectedSectionAndMakesNothingWithoutOne_PasteFallsBackToUncategorized()
    {
        var (vm, store, _, _) = Create(CommandsScope.Global);
        var media = Section(vm, "Media");

        vm.Handle(new CommandTreeActionEventArgs(CommandTreeAction.Select, media));
        vm.Handle(new CommandTreeActionEventArgs(CommandTreeAction.NewCommand));

        Assert.Equal(Category(store, "Media").Id, Find(store, "New command 1").CategoryId);
        Assert.True(Section(vm, "Media").IsExpanded);
        Assert.Equal(media.Id, vm.SelectedSectionId);
        Assert.Equal(Find(store, "New command 1").Id, vm.SelectedCommandId);

        vm.Handle(new CommandTreeActionEventArgs(CommandTreeAction.Copy, Section(vm, "Uncategorized"), Item(vm, "Three steps")));
        vm.Handle(new CommandTreeActionEventArgs(CommandTreeAction.Paste, Section(vm, "Window")));

        var pasted = Find(store, "Three steps (2)");
        Assert.Equal(Category(store, "Window").Id, pasted.CategoryId);
        Assert.Equal(pasted.Id, vm.SelectedCommandId);
        Assert.Equal(Section(vm, "Window").Id, vm.SelectedSectionId);
        Assert.True(Section(vm, "Window").IsExpanded);

        // Nothing selected (the tab's start, Joel 2026-10-11): New command makes nothing and says why.
        vm.Handle(new CommandTreeActionEventArgs(CommandTreeAction.Select));
        var count = store.Global.Commands.Count;
        vm.Handle(new CommandTreeActionEventArgs(CommandTreeAction.NewCommand));

        Assert.Equal(count, store.Global.Commands.Count);
        Assert.Equal("Select a category first, or make one with New category.", vm.Message);
        Assert.Null(vm.SelectedSectionId);

        // Uncategorized selected: there, like any category.
        vm.Handle(new CommandTreeActionEventArgs(CommandTreeAction.Select, Section(vm, "Uncategorized")));
        vm.Handle(new CommandTreeActionEventArgs(CommandTreeAction.NewCommand));

        Assert.Equal(CategoryRules.UncategorizedId, Find(store, "New command 2").CategoryId);
        Assert.Equal(Section(vm, "Uncategorized").Id, vm.SelectedSectionId);
        Assert.True(Section(vm, "Uncategorized").IsExpanded);

        // Paste with nothing selected lands in Uncategorized, which opens and holds the selection.
        vm.Handle(new CommandTreeActionEventArgs(CommandTreeAction.ToggleExpanded, Section(vm, "Uncategorized")));
        vm.Handle(new CommandTreeActionEventArgs(CommandTreeAction.Select));
        vm.Handle(new CommandTreeActionEventArgs(CommandTreeAction.Paste));

        Assert.Equal(CategoryRules.UncategorizedId, Find(store, "Three steps (3)").CategoryId);
        Assert.Equal(Find(store, "Three steps (3)").Id, vm.SelectedCommandId);
        Assert.Equal(Section(vm, "Uncategorized").Id, vm.SelectedSectionId);
        Assert.True(Section(vm, "Uncategorized").IsExpanded);
    }

    [AvaloniaFact]
    public void TheHeadersCategoryChoiceMovesTheCommandAndOpensTheSectionItWentTo()
    {
        var (vm, store, _, _) = Create(CommandsScope.Global);
        var close = Item(vm, "Close window");
        vm.Handle(new CommandTreeActionEventArgs(CommandTreeAction.Select, Section(vm, "Window"), close));

        vm.Handle(new CommandTreeActionEventArgs(CommandTreeAction.SetCategory, command: close, category: close.Categories.Single(choice => choice.Name == "Media")));

        Assert.Equal(Category(store, "Media").Id, Find(store, "Close window").CategoryId);
        Assert.True(Section(vm, "Media").IsExpanded);
        Assert.Equal(Section(vm, "Media").Id, vm.SelectedSectionId);
        Assert.Equal(close.Id, vm.SelectedCommandId);
        Assert.Equal(Category(store, "Media").Id, vm.SelectedCommand!.CategoryId);

        vm.Handle(new CommandTreeActionEventArgs(CommandTreeAction.SetCategory, command: vm.SelectedCommand, category: vm.SelectedCommand.Categories.Single(choice => choice.Name == "Uncategorized")));

        Assert.Equal(CategoryRules.UncategorizedId, Find(store, "Close window").CategoryId);
        Assert.Equal(Section(vm, "Uncategorized").Id, vm.SelectedSectionId);
        Assert.True(Section(vm, "Uncategorized").IsExpanded);

        vm.Handle(new CommandTreeActionEventArgs(CommandTreeAction.Undo));
        Assert.Equal(Category(store, "Media").Id, Find(store, "Close window").CategoryId);

        // On the Apps tab the tag follows, and "No category" (not Uncategorized) clears it.
        var (apps, appStore, _, _) = Create();
        var brush = Item(apps, "Brush");
        Assert.Equal([CategoryChoice.NoneName, "Blend Mode Normal", "General"], brush.Categories.Select(choice => choice.Name));
        apps.Handle(new CommandTreeActionEventArgs(CommandTreeAction.SetCategory, command: brush, category: brush.Categories.Single(choice => choice.Name == "Blend Mode Normal")));

        Assert.Equal("Blend Mode Normal", Item(apps, "Brush").CategoryLabel);
        Assert.Equal("Blend Mode Normal", appStore.FindGroup(brush.GroupId)!.FindCategory(Find(appStore, "Brush").CategoryId!.Value)!.Name);

        apps.Handle(new CommandTreeActionEventArgs(CommandTreeAction.SetCategory, command: Item(apps, "Brush"), category: CategoryChoice.None));

        Assert.Null(Find(appStore, "Brush").CategoryId);
        Assert.Null(Item(apps, "Brush").CategoryLabel);
        Assert.DoesNotContain(appStore.FindGroup(brush.GroupId)!.Categories, category => category.Name == "Uncategorized");
    }
}
