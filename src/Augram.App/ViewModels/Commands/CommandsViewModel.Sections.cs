using Augram.App.Components.CommandTree;

namespace Augram.App.ViewModels.Commands;

/// <summary>
/// The section half of <see cref="CommandsViewModel"/>: a section's intents go to what it is (an app
/// group on the Apps tab; a category, or Uncategorized, on the Global tab), and the expanded set is kept
/// here. What a section allows comes from its <see cref="SectionItem"/>, which <see cref="CommandSections"/> built.
/// </summary>
public sealed partial class CommandsViewModel
{
    private void ToggleExpanded(SectionItem section)
    {
        if (!_expanded.Remove(section.Id))
        {
            _expanded.Add(section.Id);
        }

        Project();
    }

    /// <summary>A category named in place on the Global tab; the app group form on the Apps tab.</summary>
    private void NewSection()
    {
        if (Scope == CommandsScope.Global)
        {
            NewCategory();
        }
        else
        {
            _ = NewGroupAsync();
        }
    }

    private void RenameSection(SectionItem section, string name)
    {
        if (section.Id.CategoryId is { } category)
        {
            RenameCategory(category, name);
        }
        else if (section.CanRename)
        {
            RenameGroup(section.Id.GroupId, name);
        }
        else
        {
            Message = $"'{section.Name}' cannot be renamed.";
        }
    }

    private Task DeleteSectionAsync(SectionItem section)
    {
        if (section.Id.CategoryId is { } category)
        {
            return DeleteCategoryAsync(category);
        }

        if (section.CanDelete)
        {
            return DeleteGroupAsync(section);
        }

        Message = $"'{section.Name}' cannot be deleted.";
        return Task.CompletedTask;
    }

    /// <summary>Only an app group has an active flag on its header; a category has none.</summary>
    private void ToggleSectionActive(SectionItem section)
    {
        if (section.CanToggleActive)
        {
            _store.UpdateGroup(RequireGroup(section.Id.GroupId) with { IsActive = !section.IsActive });
        }
    }
}
