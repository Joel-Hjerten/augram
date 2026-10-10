using Augram.App.Transfer;
using Augram.App.ViewModels;

namespace Augram.App.Tests.Transfer;

/// <summary>Answers the review as <see cref="OnReview"/> says (Import by default) and records every message; never opens a window.</summary>
internal sealed class FakeAugramImportWindows : IAugramImportWindows
{
    public Action<AugramImportViewModel> OnReview { get; set; } = review => review.Import();

    public List<AugramImportViewModel> Reviews { get; } = [];

    public List<(string Title, string Message)> Messages { get; } = [];

    public Task ShowReviewAsync(AugramImportViewModel review)
    {
        Reviews.Add(review);
        OnReview(review);
        return Task.CompletedTask;
    }

    public Task ShowMessageAsync(string title, string message)
    {
        Messages.Add((title, message));
        return Task.CompletedTask;
    }
}
