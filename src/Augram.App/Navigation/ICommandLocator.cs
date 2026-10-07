using Augram.Core.Mapping;

namespace Augram.App.Navigation;

/// <summary>
/// Takes the user to one command on the Commands tab (F3 "Used by…" rows, the delete warning). The
/// Commands tab registers the implementation; callers resolve it as optional so a build without the
/// tab (gallery, tests) still works. Called on the UI thread.
/// </summary>
public interface ICommandLocator
{
    /// <summary>Selects the Commands tab, expands the command's group and selects the command; a false return means it no longer exists.</summary>
    bool ShowCommand(CommandId id);
}
