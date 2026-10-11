using Augram.App.Dialogs;
using Microsoft.Extensions.DependencyInjection;

namespace Augram.App.Hosting.Modules;

/// <summary>
/// The dialogs every slice shares, registered once: the confirmation (<see cref="IConfirmPresenter"/>: Commands, Exclusions,
/// the Gestures tab's delete, the export and import messages), the take-over's confirmation shown alone (keyed
/// <see cref="ConfirmPresenter.AloneKey"/>: the one service a launch that must first settle with another Augram resolves) and
/// the form dialog (<see cref="IFormDialogPresenter"/>: Commands, Exclusions, export, the sync join). A slice's own presenters
/// stay in its module.
/// </summary>
public static class DialogsModule
{
    public static IServiceCollection Register(IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.AddKeyedSingleton<IConfirmPresenter>(ConfirmPresenter.AloneKey, (_, _) => ConfirmPresenter.Alone());
        services.AddSingleton<IConfirmPresenter, ConfirmPresenter>();
        services.AddSingleton<IFormDialogPresenter, FormDialogPresenter>();
        return services;
    }
}
