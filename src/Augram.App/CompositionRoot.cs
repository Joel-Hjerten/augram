using Augram.App.Views;
using Microsoft.Extensions.DependencyInjection;

namespace Augram.App;

/// <summary>
/// The single place where services are registered (ADR-0002 §2). Core stores,
/// Engine services and Platform adapters are added here as they land; nothing
/// else in the app calls <c>new</c> on a service.
/// </summary>
internal static class CompositionRoot
{
    public static ServiceProvider Build()
    {
        var services = new ServiceCollection();

        // M0: no runtime services yet. Only the shell window is resolvable.
        services.AddTransient<MainWindow>();

        return services.BuildServiceProvider();
    }
}
