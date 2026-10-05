using Augram.App.Hosting;

namespace Augram.App.Tests.Support;

public sealed class FakeClipboard : IClipboardText
{
    public string? Text { get; private set; }

    public Task SetTextAsync(string text)
    {
        Text = text;
        return Task.CompletedTask;
    }
}
