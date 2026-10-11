using Augram.App.Dialogs;
using Augram.App.ViewModels;

namespace Augram.App.Transfer;

/// <summary>
/// Shows the import's windows over the main window (modal when it is open, otherwise on their own and activated, through
/// <see cref="DialogHost"/>): the review in an <see cref="AugramImportWindow"/>, a message through
/// <see cref="IConfirmPresenter.InformAsync"/> (the shared <see cref="ConfirmDialog"/> with one OK button).
/// </summary>
public sealed class AugramImportWindows : IAugramImportWindows
{
    private readonly IConfirmPresenter _messages;

    public AugramImportWindows(IConfirmPresenter messages)
    {
        ArgumentNullException.ThrowIfNull(messages);
        _messages = messages;
    }

    public Task ShowReviewAsync(AugramImportViewModel review)
    {
        ArgumentNullException.ThrowIfNull(review);
        return DialogHost.ShowModalAsync(new AugramImportWindow(review), activateWhenAlone: true);
    }

    public Task ShowMessageAsync(string title, string message) => _messages.InformAsync(title, message);
}
