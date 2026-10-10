using Augram.App.Transfer;
using Augram.Core.Transfer;

namespace Augram.App.Tests.Transfer;

/// <summary>Records each export's preselected scope and answers <see cref="Outcome"/> at once.</summary>
internal sealed class FakeExportPresenter : IExportPresenter
{
    public List<ExportScope> Starts { get; } = [];

    public string? Outcome { get; set; } = "Exported.";

    public Task<string?> ExportAsync(ExportScope start)
    {
        Starts.Add(start);
        return Task.FromResult(Outcome);
    }
}
