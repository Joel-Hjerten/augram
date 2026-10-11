using Augram.App.Transfer;

namespace Augram.App.Tests.Transfer;

/// <summary>Answers the pickers with fixed paths (null is Cancel) and records the suggested save names and the open requests; never opens a dialog.</summary>
internal sealed class FakeTransferFilePicker : ITransferFilePicker
{
    public string? SavePath { get; set; }

    public string? OpenPath { get; set; }

    public List<string> SuggestedNames { get; } = [];

    public List<FileOpenRequest> OpenRequests { get; } = [];

    public int Opened => OpenRequests.Count;

    public Task<string?> PickSaveAsync(string suggestedFileName)
    {
        SuggestedNames.Add(suggestedFileName);
        return Task.FromResult(SavePath);
    }

    public Task<string?> PickOpenAsync(FileOpenRequest request)
    {
        OpenRequests.Add(request);
        return Task.FromResult(OpenPath);
    }
}
