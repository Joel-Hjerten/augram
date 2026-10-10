using Augram.App.Components.FormDialog;
using Augram.App.Import;
using Augram.App.Transfer;
using Augram.App.ViewModels;
using Augram.Core.Abstractions;
using Augram.Core.Config;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Augram.App.Hosting;

/// <summary>
/// The export and import slice of the composition root (plan 0003 steps 3–4): the file picker, the export and import
/// presenters, the import's windows and Options › Configuration's view model. Expects the <see cref="ConfigSession"/> (the engine slice), the
/// <see cref="IImportPresenter"/> (the Gestures slice) and <see cref="IEventLog"/>; registers the shared form dialog
/// presenter only when the Commands slice has not. The Commands and Gestures tabs find <see cref="IExportPresenter"/> when
/// they are resolved, so the order of the slices does not matter; register it after <see cref="GesturesModule"/>.
/// </summary>
public static class TransferModule
{
    public static IServiceCollection Register(IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.TryAddSingleton<IFormDialogPresenter, FormDialogPresenter>();
        services.AddSingleton<ITransferFilePicker, TransferFilePicker>();
        services.AddSingleton<IExportPresenter>(sp => new ExportPresenter(
            sp.GetRequiredService<ConfigSession>(),
            sp.GetRequiredService<IFormDialogPresenter>(),
            sp.GetRequiredService<ITransferFilePicker>(),
            sp.GetRequiredService<IEventLog>()));
        services.AddSingleton<IAugramImportWindows, AugramImportWindows>();
        services.AddSingleton<IAugramImportPresenter>(sp => new AugramImportPresenter(
            sp.GetRequiredService<ConfigSession>(),
            sp.GetRequiredService<ITransferFilePicker>(),
            sp.GetRequiredService<IAugramImportWindows>(),
            sp.GetRequiredService<IEventLog>()));
        services.AddSingleton<ConfigurationViewModel>();
        return services;
    }
}
