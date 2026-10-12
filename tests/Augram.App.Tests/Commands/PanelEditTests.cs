using Augram.App.ViewModels;
using CommunityToolkit.Mvvm.ComponentModel;
using Xunit;

namespace Augram.App.Tests.Commands;

/// <summary>
/// The side panel's form lifecycle (<see cref="PanelEdit{TEdit, TId}"/>): the form is rebuilt only when another item is
/// selected, so typing keeps focus; for the same item the stored values are synced in, and a sync is never taken for an edit.
/// </summary>
public sealed partial class PanelEditTests
{
    [Fact]
    public void TheSameItemKeepsItsFormAndSyncsIt_AnotherItemGetsANewOne()
    {
        var edits = new List<(int Id, string? Property)>();
        var panel = new PanelEdit<Form, int>((_, id, property) => edits.Add((id, property)));

        var first = panel.Show(1, () => new Form(), form => form.Name = "never");
        Assert.NotNull(first);

        first.Name = "Typed";
        Assert.Equal([(1, nameof(Form.Name))], edits);

        Assert.Null(panel.Show(1, () => new Form(), form => form.Name = "Stored"));
        Assert.Same(first, panel.Edit);
        Assert.Equal("Stored", first.Name);
        Assert.Single(edits);

        var second = panel.Show(2, () => new Form(), form => { });
        Assert.NotNull(second);
        Assert.NotSame(first, second);
        first.Name = "Old form";
        Assert.Single(edits);
    }

    private sealed partial class Form : ObservableObject
    {
        [ObservableProperty]
        public partial string Name { get; set; } = string.Empty;
    }
}
