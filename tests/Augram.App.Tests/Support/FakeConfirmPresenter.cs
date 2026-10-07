using Augram.App.UsedBy;

namespace Augram.App.Tests.Support;

/// <summary>Answers every question with <see cref="Answer"/> at once and records what was asked.</summary>
public sealed class FakeConfirmPresenter : IConfirmPresenter
{
    public bool Answer { get; set; } = true;

    public List<(string Title, string Message, string ConfirmLabel)> Requests { get; } = [];

    public Task<bool> ConfirmAsync(string title, string message, string confirmLabel)
    {
        Requests.Add((title, message, confirmLabel));
        return Task.FromResult(Answer);
    }
}
