using Augram.App.Components.CommandTree;
using Augram.Core.Transfer;

namespace Augram.App.ViewModels.Commands;

/// <summary>
/// Export… on a section's menu (plan 0003 step 3): the export dialog with the section's app group preselected; a category of
/// Global preselects Global, since a group is exported whole. The outcome ("Exported …", "Could not save
/// …") goes to the message line; Cancel leaves it alone (<see cref="StoreViewModel.ExportAsync"/>).
/// </summary>
public sealed partial class CommandsViewModel
{
    private Task ExportAsync(SectionItem section) => ExportAsync(ExportScope.Of([section.Id.GroupId]));
}
