using Augram.App.Components.Fields;
using Augram.App.Declarations;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using Xunit;

namespace Augram.App.Tests.Components;

/// <summary>The committed text field (Options › Sync's repository address and machine name): drafts per keystroke, commit on leaving or Enter, revert on Escape.</summary>
public sealed class CommittedTextFieldTests
{
    [AvaloniaFact]
    public void TheEditorReportsDraftsAndCommitsOnLeavingOrEnterAndRevertsOnEscape()
    {
        var draft = "start";
        var problem = string.Empty;
        var commits = 0;
        var reverts = 0;
        var editor = FieldRendererRegistry.Default.Build(new CommittedTextField(
            "Repository",
            new DelegateBinding<string>(() => draft, value => draft = value),
            new DelegateBinding<string>(() => problem),
            () => commits++,
            () => reverts++)
        { Wide = true });
        new Window { Content = editor }.Show();
        var box = editor.GetVisualDescendants().OfType<TextBox>().Single();
        var error = editor.GetVisualDescendants().OfType<TextBlock>().Single(text => text.Classes.Contains("error"));

        Assert.Equal("start", box.Text);
        Assert.Contains("wide", box.Classes);
        Assert.False(error.IsVisible);
        box.Text = "typed";
        Assert.Equal("typed", draft);
        Assert.Equal(0, commits);

        box.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = Key.Enter });
        Assert.Equal(1, commits);
        box.RaiseEvent(new RoutedEventArgs(InputElement.LostFocusEvent));
        Assert.Equal(2, commits);
        box.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = Key.Escape });
        Assert.Equal(1, reverts);
    }
}
