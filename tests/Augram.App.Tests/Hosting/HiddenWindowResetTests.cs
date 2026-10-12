using Augram.App.Hosting;
using Augram.App.Hosting.Modules;
using Augram.App.Tests.Support;
using Augram.App.ViewModels;
using Augram.App.ViewModels.Commands;
using Augram.App.ViewModels.Ignored;
using Augram.Core.Gestures;
using Augram.Core.Mapping;
using Augram.Core.Recognition;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Augram.App.Tests.Hosting;

/// <summary>Hiding the main window empties every list's search box (plan 0009 decision 8); a tab not registered is skipped.</summary>
public sealed class HiddenWindowResetTests
{
    [AvaloniaFact]
    public void HidingTheWindowClearsEveryTabsSearchText_SkippingTabsNotRegistered()
    {
        var services = new ServiceCollection();
        services.AddSingleton(new MappingStore());
        services.AddSiblings();
        // The Gestures tab over fakes: the module's own needs the import presenter's file picker and log.
        services.AddSingleton(sp => new GesturesViewModel(sp.GetRequiredService<GestureLibrary>(), () => RecognitionOptions.Default, new FakeTrainingPresenter(), new FakeImportPresenter(), sp.GetRequiredService<MappingStore>(), new FakeUsedByPresenter(), new FakeConfirmPresenter()));
        CommandsModule.Register(services);
        IgnoredModule.Register(services);
        using var provider = services.BuildServiceProvider();
        var global = provider.GetRequiredKeyedService<CommandsViewModel>(CommandsScope.Global);
        var apps = provider.GetRequiredKeyedService<CommandsViewModel>(CommandsScope.Apps);
        var ignored = provider.GetRequiredKeyedService<IgnoredViewModel>(IgnoreScope.PerCommand);
        var gestures = provider.GetRequiredService<GesturesViewModel>();
        global.SearchText = "a";
        apps.SearchText = "b";
        ignored.SearchText = "c";
        gestures.SearchText = "d";
        var window = new Window();
        HiddenWindowReset.Follow(window, provider);
        window.Show();
        Assert.Equal("a", global.SearchText);

        window.Hide();

        Assert.Equal(["", "", "", ""], new[] { global.SearchText, apps.SearchText, ignored.SearchText, gestures.SearchText });
        Assert.Null(provider.GetService<LogViewModel>());
    }
}
