using Augram.App.Transfer;

namespace Augram.App.Tests.Transfer;

/// <summary>Answers the pickers with fixed paths (null is Cancel) and records the suggested save names; never opens a dialog.</summary>
internal sealed class FakeTransferFilePicker : ITransferFilePicker
{
    public string? SavePath { get; set; }

    public string? OpenPath { get; set; }

    public List<string> SuggestedNames { get; } = [];

    public int Opened { get; private set; }

    public Task<string?> PickSaveAsync(string suggestedFileName)
    {
        SuggestedNames.Add(suggestedFileName);
        return Task.FromResult(SavePath);
    }

    public Task<string?> PickOpenAsync()
    {
        Opened++;
        return Task.FromResult(OpenPath);
    }
}
