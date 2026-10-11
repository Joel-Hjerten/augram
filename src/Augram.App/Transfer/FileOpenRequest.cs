using Avalonia.Platform.Storage;

namespace Augram.App.Transfer;

/// <summary>
/// What an open picker shows (<see cref="ITransferFilePicker.PickOpenAsync"/>): its title, its filters in order, the file name
/// it suggests and where it starts. With <see cref="FollowsLastFolder"/> (the Augram file dialogs) it starts in, and
/// remembers, the folder the last export or import of this run was in; otherwise it starts in <see cref="StartFolder"/> (the
/// platform's choice when null) and remembers nothing. The Augram import asks with <see cref="TransferFilePicker.OpenAugramFile"/>,
/// the StrokesPlus.net import with <see cref="Import.ImportPresenter.OpenRequest"/>.
/// </summary>
public sealed record FileOpenRequest(string Title, IReadOnlyList<FilePickerFileType> Filters)
{
    public string? SuggestedFileName { get; init; }

    public string? StartFolder { get; init; }

    public bool FollowsLastFolder { get; init; }
}
