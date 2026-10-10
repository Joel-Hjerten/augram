using Augram.App.Components.SectionForm;
using Augram.App.ViewModels;
using Augram.Core.Gestures;
using Augram.Core.Mapping;
using Augram.Core.Transfer;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.VisualTree;
using Xunit;
using static Augram.App.Tests.Transfer.TransferTestData;

namespace Augram.App.Tests.Transfer;

/// <summary>
/// The export dialog's view model (plan 0003 step 3): the scope each entry point preselects, the selection list, what the
/// file will hold and the typed-text note, an empty selection, the suggested name, and the declared form.
/// </summary>
public sealed class ExportViewModelTests
{
    private static readonly int StarterCount = StarterGestures.All().Count;

    [Fact]
    public void StartsOnTheScopeItsEntryPointPreselects()
    {
        var current = Configuration();
        var blender = GroupNamed(current.Mapping, "Blender");

        Assert.Equal(ExportKind.Everything, new ExportViewModel(current, ExportScope.Everything).Kind);
        Assert.Equal(ExportKind.GesturesOnly, new ExportViewModel(current, ExportScope.GesturesOnly).Kind);

        var group = new ExportViewModel(current, ExportScope.Of([blender.Id]));
        Assert.Equal(ExportKind.Selected, group.Kind);
        Assert.Equal(["Blender"], group.Items.Where(group.IsChecked).Select(item => item.Name));
        var selection = Assert.IsType<ExportScope.Selection>(group.Scope);
        Assert.Equal([blender.Id], selection.Groups);

        var category = new ExportViewModel(current, ExportScope.Of([GroupId.Global]));
        Assert.Equal([AppGroup.GlobalName], category.Items.Where(category.IsChecked).Select(item => item.Name));
    }

    [Fact]
    public void ListsGlobalFirst_ThenTheAppGroupsByName_ThenTheIgnoredApps()
    {
        var vm = new ExportViewModel(Configuration(), ExportScope.Everything);

        Assert.Equal(["Global", "Blender", "Chrome", "Game"], vm.Items.Select(item => item.Name));
        Assert.Equal(["2 commands", "2 commands · 1 hold remap · Windows only", "2 commands", "ignored app"], vm.Items.Select(item => item.Detail));
        Assert.Equal([false, false, false, true], vm.Items.Select(item => item.IsIgnoredApp));
        Assert.DoesNotContain(vm.Items, vm.IsChecked);
    }

    [Fact]
    public void WhatTheFileHolds_AndTheTypedTextNote_FollowTheChoice()
    {
        var vm = new ExportViewModel(Configuration(), ExportScope.Everything);

        Assert.Equal($"options, {StarterCount} gestures, Global, 2 app groups, 1 hold remap, 6 commands, 1 ignored app", vm.ContentsText);
        Assert.True(vm.HasPrivateText);
        Assert.Equal("2 steps type text or run command lines; they are in the file as written. Do not share it if they hold passwords.", vm.PrivateTextNote);

        vm.Kind = ExportKind.GesturesOnly;
        Assert.Equal($"{StarterCount} gestures", vm.ContentsText);
        Assert.False(vm.HasPrivateText);

        vm.Kind = ExportKind.Selected;
        vm.SetChecked(vm.Items.Single(item => item.Name == "Blender"), true);
        Assert.Equal("1 gesture, 1 app group, 1 hold remap, 2 commands", vm.ContentsText);
        Assert.False(vm.HasPrivateText);

        vm.SetChecked(vm.Items.Single(item => item.Name == "Chrome"), true);
        Assert.Equal("2 gestures, 2 app groups, 1 hold remap, 4 commands", vm.ContentsText);
        Assert.True(vm.HasPrivateText);
        Assert.Equal("1 step types text or runs a command line; it is in the file as written. Do not share it if it holds a password.", vm.PrivateTextNote);
    }

    [Fact]
    public void AnEmptySelectionCannotBeSaved_UntilSomethingIsTicked()
    {
        var current = Configuration();
        var vm = new ExportViewModel(current, ExportScope.Of([]));

        Assert.False(vm.CanSave);
        Assert.Equal(ExportViewModel.NothingSelectedText, vm.ContentsText);
        Assert.False(vm.HasPrivateText);

        var game = vm.Items.Single(item => item.IsIgnoredApp);
        vm.SetChecked(game, true);
        Assert.True(vm.CanSave);
        var selection = Assert.IsType<ExportScope.Selection>(vm.Scope);
        Assert.Empty(selection.Groups);
        Assert.Equal([game.Id], selection.Ignored);
        Assert.Equal("Game", Assert.Single(vm.Export().Mapping!.Ignored).Name);

        vm.Kind = ExportKind.Everything;
        vm.SetChecked(game, false);
        Assert.True(vm.CanSave);
    }

    [Fact]
    public void TheSuggestedNameSaysWhatIsExported()
    {
        var current = Configuration();
        var today = new DateOnly(2026, 10, 10);

        Assert.Equal("Augram everything 2026-10-10.augram.json", new ExportViewModel(current, ExportScope.Everything).SuggestedFileName(today));
        Assert.Equal("Augram gestures 2026-10-10.augram.json", new ExportViewModel(current, ExportScope.GesturesOnly).SuggestedFileName(today));
        Assert.Equal("Augram Blender 2026-10-10.augram.json", new ExportViewModel(current, ExportScope.Of([GroupNamed(current.Mapping, "Blender").Id])).SuggestedFileName(today));
    }

    [AvaloniaFact]
    public void TheFormShowsTheListOnlyUnderSelected_AndItsBoxesTickTheSelection()
    {
        var vm = new ExportViewModel(Configuration(), ExportScope.GesturesOnly);
        var form = new SectionForm { Screen = vm.Declare() };
        var window = new Window { Width = 700, Height = 700, Content = form };
        window.Show();
        var rows = form.GetVisualDescendants().OfType<FieldRow>().ToDictionary(row => row.Label);

        Assert.Equal(["Scope", "App groups", "In the file", "Typed text"], rows.Keys);
        Assert.False(rows["App groups"].IsVisible);
        Assert.False(rows["Typed text"].IsVisible);
        var scopes = rows["Scope"].GetVisualDescendants().OfType<RadioButton>().ToList();
        Assert.Equal(["Everything", "Gestures only", "Selected"], scopes.Select(button => (string)button.Content!));

        scopes[2].IsChecked = true;
        Assert.Equal(ExportKind.Selected, vm.Kind);
        Assert.True(rows["App groups"].IsVisible);
        Assert.Equal(ExportViewModel.NothingSelectedText, ((TextBlock)rows["In the file"].Editor!).Text);

        // The list was hidden when the window was laid out; its boxes exist once it is laid out shown.
        window.UpdateLayout();
        var boxes = rows["App groups"].GetVisualDescendants().OfType<CheckBox>().ToList();
        Assert.Equal(4, boxes.Count);
        boxes[2].IsChecked = true;
        Assert.Equal(["Chrome"], vm.Items.Where(vm.IsChecked).Select(item => item.Name));
        Assert.Equal("2 gestures, 1 app group, 2 commands", ((TextBlock)rows["In the file"].Editor!).Text);
        Assert.True(rows["Typed text"].IsVisible);
    }
}
