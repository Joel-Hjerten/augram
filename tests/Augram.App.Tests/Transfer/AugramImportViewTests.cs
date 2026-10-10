using Augram.App.Components.SectionForm;
using Augram.App.Components.SyncConflictList;
using Augram.App.Screens;
using Augram.App.Tests.Support;
using Augram.App.Transfer;
using Augram.App.ViewModels;
using Augram.Core.Steps;
using Augram.Core.Sync;
using Augram.Core.Transfer;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using Xunit;
using static Augram.App.Tests.Transfer.TransferTestData;

namespace Augram.App.Tests.Transfer;

/// <summary>The rendered import review (plan 0003 step 4): the declared top, the shared conflict list headed "Yours" / "In the file", Apply to all, and Import enabled only while there is something to import.</summary>
public sealed class AugramImportViewTests
{
    [AvaloniaFact]
    public void TheReviewShowsTheDifferingItems_AppliesToAll_AndImports()
    {
        var mine = Configuration();
        using var session = Session(mine);
        var vm = new AugramImportViewModel(session, Read(Exporter.Export(ExportScope.Everything, Redrawn(mine, "Z", "S"))), "theirs.augram.json", new ListEventLog());
        var window = new AugramImportWindow(vm);
        window.Show();
        var view = window.View;
        var rows = Rows(view);

        Assert.True(view.List.IsVisible);
        Assert.Equal([AugramImportViewModel.MineHeader, AugramImportViewModel.TheirsHeader], view.List.GetVisualDescendants().OfType<TextBlock>().Where(text => text.Classes.Contains("column-header")).Skip(1).Take(2).Select(text => text.Text));
        Assert.Single(view.List.GetVisualDescendants().OfType<SyncConflictRow>());
        Assert.False(rows["Nothing to import"].IsVisible);
        Assert.True(rows["Also use its options"].IsVisible);
        Assert.Equal("theirs.augram.json", view.Form.GetVisualDescendants().OfType<SectionView>().Single().Title);
        Assert.True(view.ImportButton.IsEnabled);

        var applyToAll = rows["Every differing item"];
        applyToAll.GetVisualDescendants().OfType<ComboBox>().Single().SelectedIndex = 1;
        applyToAll.GetVisualDescendants().OfType<Button>().Single(button => Equals(button.Content, AugramImportScreen.ApplyToAllLabel)).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        window.UpdateLayout();
        Assert.Equal(SyncChoice.TakeTheirs, view.List.GetVisualDescendants().OfType<SyncConflictRow>().Single().SelectedChoice);

        view.ImportButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));

        Assert.NotNull(vm.ResultText);
        Assert.False(window.IsVisible);
        Assert.Equal(Redrawn(mine, "Z", "S").Gestures.Single(gesture => gesture.Name == "Z").Samples, session.Gestures.All.Single(gesture => gesture.Name == "Z").Samples);
    }

    [AvaloniaFact]
    public void AFileOfWhatIsAlreadyHere_SaysSo_AndCannotBeImported()
    {
        var mine = Configuration();
        using var session = Session(mine);
        var view = new AugramImportView(new AugramImportViewModel(session, Read(Exporter.Export(ExportScope.GesturesOnly, mine)), "gestures.augram.json", new ListEventLog()));
        new Window { Width = 960, Height = 640, Content = view }.Show();
        var rows = Rows(view);

        Assert.True(rows["Nothing to import"].IsVisible);
        Assert.Equal(AugramImportViewModel.EmptyText, ((TextBlock)rows["Nothing to import"].Editor!).Text);
        Assert.False(rows["Also use its options"].IsVisible);
        Assert.False(rows["Every differing item"].IsVisible);
        Assert.False(view.List.IsVisible);
        Assert.False(view.ImportButton.IsEnabled);
    }

    private static Dictionary<string, FieldRow> Rows(AugramImportView view)
        => view.Form.GetVisualDescendants().OfType<FieldRow>().ToDictionary(row => row.Label);

    private static TransferFile Read(TransferFile file) => TransferSerializer.Read(TransferSerializer.Write(file), StepRegistry.BuiltIn);
}
