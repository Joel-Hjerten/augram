using Augram.App.Dialogs;
using Augram.App.Import;
using Augram.App.Transfer;
using Augram.App.ViewModels;
using Augram.Core.Abstractions;
using Augram.Core.Config;
using Microsoft.Extensions.DependencyInjection;

namespace Augram.App.Hosting.Modules;

/// <summary>
/// The export and import slice of the composition root (plan 0003 steps 3–4): the file picker, the export and import
/// presenters, the import's windows and Options › Sync › Export and import's view model. Resolves the <see cref="ConfigSession"/>
/// (<see cref="ConfigModule"/>), the form dialog presenter (<see cref="DialogsModule"/>), the <see cref="IImportPresenter"/>
/// (<see cref="GesturesModule"/>) and <see cref="IEventLog"/>. The file picker is shared with the StrokesPlus.net import. The
/// Commands and Gestures tabs find <see cref="IExportPresenter"/> when they are resolved, so the order of the slices does not matter.
/// </summary>
public static class TransferModule
{
    public static IServiceCollection Register(IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
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
