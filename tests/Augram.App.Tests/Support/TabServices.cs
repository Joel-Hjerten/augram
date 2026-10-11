using Augram.App.Hosting.Modules;
using Augram.Core.Gestures;
using Microsoft.Extensions.DependencyInjection;

namespace Augram.App.Tests.Support;

/// <summary>
/// What a tab module resolves from its siblings, for a test that registers only the tab (and its <c>MappingStore</c>): the
/// starter gestures in place of <see cref="ConfigModule"/>'s library, the shared dialogs and the Gestures slice (training).
/// </summary>
internal static class TabServices
{
    public static IServiceCollection AddSiblings(this IServiceCollection services)
    {
        services.AddSingleton(new GestureLibrary(StarterGestures.All()));
        DialogsModule.Register(services);
        GesturesModule.Register(services);
        return services;
    }
}
