using Augram.App.Transfer;

namespace Augram.App.Tests.Transfer;

/// <summary>Counts the imports started and answers <see cref="Outcome"/> at once.</summary>
internal sealed class FakeAugramImportPresenter : IAugramImportPresenter
{
    public int Opened { get; private set; }

    public string? Outcome { get; set; } = "Imported.";

    public Task<string?> OpenAsync()
    {
        Opened++;
        return Task.FromResult(Outcome);
    }
}
