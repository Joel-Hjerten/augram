using Augram.App.Components.CommandTree;
using Augram.App.Tests.Support;
using Augram.App.ViewModels.Commands;
using Augram.Core.Mapping;
using Avalonia.Headless.XUnit;
using Xunit;
using static Augram.App.Tests.Commands.CommandsTestData;

namespace Augram.App.Tests.Commands;

/// <summary>The Global tab's view model: the Global group's categories as sections (Joel, 2026-10-07), Uncategorized first while it has commands.</summary>
public sealed class CommandsViewModelGlobalTests
{
    [AvaloniaFact]
    public void SectionsAreTheCategoriesByNameAfterUncategorizedOnlyWhileItHasCommands()
    {
        var (vm, store, _, _) = Create(CommandsScope.Global);

        Assert.Equal(("Global commands", "New category…"), (vm.Heading, vm.NewSectionLabel));
        Assert.Equal(["Uncategorized", "Media", "Window"], Names(vm));
        var uncategorized = vm.Sections[0];
        Assert.Equal(SectionId.Uncategorized, uncategorized.Id);
        Assert.True(uncategorized is { CanRename: false, CanDelete: false, CanEditDefinition: false, CanToggleActive: false });
        Assert.All(vm.Sections.Skip(1), section => Assert.True(section is { CanRename: true, CanDelete: true, CanEditDefinition: false, CanToggleActive: false }));
        Assert.Equal(["Three steps"], uncategorized.Commands.Select(command => command.Name));
        Assert.Equal(["Close window", "Minimize"], Section(vm, "Window").Commands.Select(command => command.Name));
        Assert.Equal(SectionId.ForCategory(GroupId.Global, Category(store, "Window").Id), Section(vm, "Window").Id);

        var close = Item(vm, "Close window");
        Assert.Equal(("Close window", "Up", true), (close.StepSummary, close.TriggerText, close.HasGlyph));
        var volume = Item(vm, "Volume up");
        Assert.Equal((TriggerKind.WheelUp, "Wheel up", false), (volume.TriggerKind, volume.TriggerText, volume.HasGlyph));
        Assert.Equal("3 steps", Item(vm, "Three steps").StepSummary);
        Assert.All(vm.Sections.SelectMany(section => section.Commands), command =>
        {
            Assert.Null(command.CategoryLabel);
            Assert.Equal(["Uncategorized", "Media", "Window"], command.Categories.Select(choice => choice.Name));
        });

        var three = Item(vm, "Three steps");
        vm.Handle(new CommandTreeActionEventArgs(CommandTreeAction.SetCategory, command: three, category: three.Categories.Single(choice => choice.Name == "Media")));

        Assert.Equal(["Media", "Window"], Names(vm));
        Assert.Equal(["Three steps", "Volume up"], Section(vm, "Media").Commands.Select(command => command.Name));
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
        Assert.Equal(["Uncategorized", "Media", "New category 1", "Window"], Names(vm));

        vm.Handle(new CommandTreeActionEventArgs(CommandTreeAction.NewSection));
        Assert.Equal(["Media", "New category 1", "New category 2", "Window"], store.Global.Categories.Select(category => category.Name).Order(StringComparer.Ordinal));

        vm.Handle(new CommandTreeActionEventArgs(CommandTreeAction.Undo));
        Assert.Equal(["Uncategorized", "Media", "New category 1", "Window"], Names(vm));
    }

    [AvaloniaFact]
    public void RenameCategoryGoesThroughTheStoreAndUncategorizedIsNeverRenamed()
    {
        var (vm, store, _, _) = Create(CommandsScope.Global);

        vm.Handle(new CommandTreeActionEventArgs(CommandTreeAction.Rename, Section(vm, "Media"), null, "Sound"));

        Assert.Equal(["Uncategorized", "Sound", "Window"], Names(vm));
        Assert.Equal(Category(store, "Sound").Id, Find(store, "Volume up").CategoryId);
        Assert.Equal(["Volume up"], Section(vm, "Sound").Commands.Select(command => command.Name));

        vm.Handle(new CommandTreeActionEventArgs(CommandTreeAction.Rename, Section(vm, "Uncategorized"), null, "Misc"));

        Assert.Equal("'Uncategorized' cannot be renamed.", vm.Message);
        Assert.Equal(2, store.Global.Categories.Count);
        Assert.Equal(["Uncategorized", "Sound", "Window"], Names(vm));
    }

    [AvaloniaFact]
    public void DeleteCategoryAsksThenMovesItsCommandsToUncategorizedInOneUndoStep()
    {
        var confirm = new FakeConfirmPresenter { Answer = false };
        var (vm, store, _, _) = Create(CommandsScope.Global, confirm);

        vm.Handle(new CommandTreeActionEventArgs(CommandTreeAction.Delete, Section(vm, "Window")));

        Assert.Equal(("Delete category", "Delete category 'Window'? Its 2 commands move to Uncategorized.", "Delete"), confirm.Requests[^1]);
        Assert.Equal(2, store.Global.Categories.Count);

        confirm.Answer = true;
        vm.Handle(new CommandTreeActionEventArgs(CommandTreeAction.Delete, Section(vm, "Window")));

        Assert.Equal(["Media"], store.Global.Categories.Select(category => category.Name));
        Assert.Null(Find(store, "Close window").CategoryId);
        Assert.Null(Find(store, "Minimize").CategoryId);
        Assert.Equal(["Uncategorized", "Media"], Names(vm));
        Assert.Equal(["Close window", "Minimize", "Three steps"], Section(vm, "Uncategorized").Commands.Select(command => command.Name));
        Assert.Equal($"Deleted 'Window'. {CommandsKeymap.Current.Undo} undoes it.", vm.Message);

        vm.Handle(new CommandTreeActionEventArgs(CommandTreeAction.Undo));

        Assert.Equal(["Uncategorized", "Media", "Window"], Names(vm));
        Assert.Equal(["Close window", "Minimize"], Section(vm, "Window").Commands.Select(command => command.Name));

        var asked = confirm.Requests.Count;
        vm.Handle(new CommandTreeActionEventArgs(CommandTreeAction.Delete, Section(vm, "Uncategorized")));
        Assert.Equal("'Uncategorized' cannot be deleted.", vm.Message);
        Assert.Equal(asked, confirm.Requests.Count);

        vm.Handle(new CommandTreeActionEventArgs(CommandTreeAction.Delete, Section(vm, "Media")));
        Assert.Equal("Delete category 'Media'? Its command moves to Uncategorized.", confirm.Requests[^1].Message);
    }

    [AvaloniaFact]
    public void NewCommandAndPasteLandInTheSelectedCategoryOrUncategorizedWithoutOne()
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

        var pasted = Find(store, "Three steps copy");
        Assert.Equal(Category(store, "Window").Id, pasted.CategoryId);
        Assert.Equal(pasted.Id, vm.SelectedCommandId);
        Assert.Equal(Section(vm, "Window").Id, vm.SelectedSectionId);
        Assert.True(Section(vm, "Window").IsExpanded);

        vm.Handle(new CommandTreeActionEventArgs(CommandTreeAction.Select));
        vm.Handle(new CommandTreeActionEventArgs(CommandTreeAction.NewCommand));

        Assert.Null(Find(store, "New command 2").CategoryId);
        Assert.Equal(SectionId.Uncategorized, vm.SelectedSectionId);
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

        vm.Handle(new CommandTreeActionEventArgs(CommandTreeAction.SetCategory, command: vm.SelectedCommand, category: CategoryChoice.Uncategorized));

        Assert.Null(Find(store, "Close window").CategoryId);
        Assert.Equal(SectionId.Uncategorized, vm.SelectedSectionId);
        Assert.True(Section(vm, "Uncategorized").IsExpanded);

        vm.Handle(new CommandTreeActionEventArgs(CommandTreeAction.Undo));
        Assert.Equal(Category(store, "Media").Id, Find(store, "Close window").CategoryId);

        // On the Apps tab the tag follows.
        var (apps, appStore, _, _) = Create();
        var brush = Item(apps, "Brush");
        apps.Handle(new CommandTreeActionEventArgs(CommandTreeAction.SetCategory, command: brush, category: brush.Categories.Single(choice => choice.Name == "Blend Mode Normal")));

        Assert.Equal("Blend Mode Normal", Item(apps, "Brush").CategoryLabel);
        Assert.Equal("Blend Mode Normal", appStore.FindGroup(brush.GroupId)!.FindCategory(Find(appStore, "Brush").CategoryId!.Value)!.Name);
    }
}
