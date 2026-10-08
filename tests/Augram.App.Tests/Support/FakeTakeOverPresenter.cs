using Augram.App.Hosting;

namespace Augram.App.Tests.Support;

/// <summary>Answers the take-over question with <see cref="Answer"/> at once and records what was asked and told.</summary>
internal sealed class FakeTakeOverPresenter : ITakeOverPresenter
{
    public bool Answer { get; set; } = true;

    public List<(string Title, string Message, string ConfirmLabel)> Questions { get; } = [];

    public List<(string Title, string Message)> Messages { get; } = [];

    public Task<bool> ConfirmAsync(string title, string message, string confirmLabel)
    {
        Questions.Add((title, message, confirmLabel));
        return Task.FromResult(Answer);
    }

    public Task InformAsync(string title, string message)
    {
        Messages.Add((title, message));
        return Task.CompletedTask;
    }
}
