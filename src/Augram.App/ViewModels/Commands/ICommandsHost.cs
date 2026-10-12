using Augram.App.Declarations;
using Augram.Core.Capture;
using Augram.Core.Mapping;

namespace Augram.App.ViewModels.Commands;

/// <summary>
/// What <see cref="CommandsViewModel"/> lends its collaborators (<see cref="TriggerEditor"/>, <see cref="StepEditor"/>,
/// <see cref="SectionPanel"/>): the message line and <c>Guard</c> of its <see cref="StoreViewModel"/> base, the selection it
/// keeps, and the projection calls that show an edit. The view model implements it explicitly, so none of this widens its
/// public surface; each member does exactly what the view model's own member does.
/// </summary>
internal interface ICommandsHost
{
    /// <summary>The message line (<see cref="StoreViewModel.Message"/>).</summary>
    string? Message { get; set; }

    /// <summary>This machine's stroke button, null in tests and the gallery (<see cref="CommandsViewModel.StrokeButton"/>).</summary>
    MouseButton? StrokeButton { get; }

    /// <summary>The selected command (<see cref="CommandsViewModel.SelectedCommandId"/>).</summary>
    CommandId? SelectedCommandId { get; }

    /// <summary>The selected step of the selected command, which the keys act on (<see cref="CommandsViewModel.SelectedStepIndex"/>).</summary>
    int SelectedStepIndex { get; set; }

    /// <summary>The open steps of the selected command (<see cref="CommandsViewModel.OpenStepIndexes"/>).</summary>
    IReadOnlySet<int> OpenStepIndexes { get; set; }

    /// <summary>Runs an intent; a refusal shows on the message line. False when it was refused (<c>StoreViewModel.Guard</c>).</summary>
    bool Guard(Action action);

    /// <summary>Shows the selected command in the header again, with the trigger draft over it while one waits.</summary>
    void ShowSelected();

    /// <summary>Lists the other platforms' rows too when <paramref name="otherPlatforms"/>, then opens the command's section and selects it.</summary>
    void Reveal(CommandId id, bool otherPlatforms);

    /// <summary>Puts a new side-panel form in place (<see cref="CommandsViewModel.GroupForm"/>), or none.</summary>
    void ShowForm(FormScreen? form);
}
