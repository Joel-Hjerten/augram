using Augram.App.Components.FormDialog;
using Augram.App.Dialogs;
using Augram.App.Sync;
using Augram.App.ViewModels;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Threading;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Augram.App.Tests.Dialogs;

/// <summary>The kept rules of the shared dialog pieces (plan 0007 S10). Headless tests have no desktop lifetime, so there is no main window: every dialog is shown alone.</summary>
public sealed class DialogHostTests
{
    [AvaloniaFact]
    public async Task WithoutAVisibleMainWindow_AQuestionAnswersNo()
    {
        Assert.Null(DialogHost.Owner);

        Assert.False(await new ConfirmPresenter().ConfirmAsync("Delete gesture", "Delete 'Down'?", "Delete"));
    }

    [Fact]
    public void TheTakeOverAsksThroughItsOwnPresenter_TheTabsThroughTheMainWindowOne()
    {
        var takeOver = TestAppBuilder.Services.GetRequiredKeyedService<IConfirmPresenter>(ConfirmPresenter.AloneKey);
        var tabs = TestAppBuilder.Services.GetRequiredService<IConfirmPresenter>();

        Assert.IsType<ConfirmPresenter>(takeOver);
        Assert.IsType<ConfirmPresenter>(tabs);
        Assert.NotSame(takeOver, tabs);
    }

    [AvaloniaFact]
    public void AWindowShownAloneIsActivatedOnlyWhenTheCallerAsks()
    {
        var left = Show(activateWhenAlone: false);
        Assert.False(left.IsActive);
        left.Close();

        var activated = Show(activateWhenAlone: true);
        Assert.True(activated.IsActive);
        activated.Close();
    }

    [AvaloniaFact]
    public void TheJoinQuestionIsWiderThanAFormAndActivatedWhenAlone()
    {
        var request = SyncJoinPresenter.Request(new SyncJoinViewModel([]));

        Assert.True(request.ActivateWhenAlone);
        Assert.Equal(680, new FormDialogWindow(new FormDialog(), request.Title, request.Width).Width);
        Assert.Equal(560, new FormDialogWindow(new FormDialog(), "New app group").Width);
        Assert.False(new FormDialogRequest("New app group", "Create").ActivateWhenAlone);
    }

    [AvaloniaFact]
    public void ASlotClosesTheWindowOnShowWhenAnotherOpens()
    {
        var slot = new DialogSlot();
        var first = new Window();
        var second = new Window();

        slot.Show(first);
        slot.Show(second);

        Assert.False(first.IsVisible);
        Assert.Same(second, slot.Current);
        second.Close();
        Assert.Null(slot.Current);
    }

    [AvaloniaFact]
    public void EscapeClosesTheDialogOnceAndAnswersNo()
    {
        var dialog = new ConfirmDialog("Delete gesture", "Delete 'Down'?", "Delete");
        var answer = dialog.ShowAloneAsync();
        var escape = new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = Key.Escape };

        dialog.RaiseEvent(escape);

        Assert.True(escape.Handled);
        Assert.True(answer.IsCompletedSuccessfully);
        Assert.False(answer.Result);
    }

    private static Window Show(bool activateWhenAlone)
    {
        // Not activated by its own show, so only the host's Activate can make it active.
        var window = new Window { ShowActivated = false };
        _ = DialogHost.ShowModalAsync(window, activateWhenAlone);
        Dispatcher.UIThread.RunJobs();
        return window;
    }
}
