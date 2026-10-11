using Augram.App.Dialogs;
using Augram.App.Navigation;
using Augram.Core.Gestures;
using Augram.Core.Mapping;

namespace Augram.App.UsedBy;

/// <summary>
/// Shows a gesture's commands as a <see cref="UsedByWindow"/> owned by the main window, like the
/// training popup: one at a time, opening another replaces it (<see cref="DialogSlot"/>). The <see cref="ICommandLocator"/> is
/// looked up at show time, so the popup gains "Go to command" when the Commands tab registers one and
/// works without it (gallery, tests).
/// </summary>
public sealed class UsedByPresenter : IUsedByPresenter
{
    private readonly MappingStore _mapping;
    private readonly GestureLibrary _library;
    private readonly Func<ICommandLocator?> _locator;
    private readonly DialogSlot _slot = new();

    public UsedByPresenter(MappingStore mapping, GestureLibrary library, Func<ICommandLocator?> locator)
    {
        ArgumentNullException.ThrowIfNull(mapping);
        ArgumentNullException.ThrowIfNull(library);
        ArgumentNullException.ThrowIfNull(locator);
        _mapping = mapping;
        _library = library;
        _locator = locator;
    }

    public void Show(GestureId gestureId)
    {
        _slot.Close();
        var name = _library.Find(gestureId)?.Name ?? "(deleted gesture)";
        _slot.Show(new UsedByWindow(gestureId, name, _mapping, _locator()));
    }
}
