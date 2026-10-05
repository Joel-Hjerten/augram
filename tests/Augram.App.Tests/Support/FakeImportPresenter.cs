using Augram.App.Import;

namespace Augram.App.Tests.Support;

public sealed class FakeImportPresenter : IImportPresenter
{
    public int Opened { get; private set; }

    public Task OpenAsync()
    {
        Opened++;
        return Task.CompletedTask;
    }
}
