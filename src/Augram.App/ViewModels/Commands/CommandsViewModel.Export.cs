using Augram.App.Components.CommandTree;
using Augram.Core.Transfer;

namespace Augram.App.ViewModels.Commands;

/// <summary>
/// Export… on a section's menu (plan 0003 step 3): the export dialog with the section's app group preselected; a category of
/// Global (or Uncategorized) preselects Global, since a group is exported whole. The outcome ("Exported …", "Could not save
/// …") goes to the message line; Cancel leaves it alone.
/// </summary>
public sealed partial class CommandsViewModel
{
    private async Task ExportAsync(SectionItem section)
    {
        if (_export is null)
        {
            Message = "Export is not available here.";
            return;
        }

        if (await _export.ExportAsync(ExportScope.Of([section.Id.GroupId])).ConfigureAwait(true) is { } outcome)
        {
            Message = outcome;
        }
    }
}
