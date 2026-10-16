using Augram.App.ViewModels;
using Augram.App.ViewModels.Commands;
using Augram.App.ViewModels.Ignored;
using Augram.Core.Mapping;
using Avalonia;
using Avalonia.Controls;
using Microsoft.Extensions.DependencyInjection;

namespace Augram.App.Hosting;

/// <summary>
/// What hiding the main window resets (plan 0009 decision 8): every list's search text. The text is UI-only state that is
/// never persisted, and the tabs' view models are process-lifetime singletons, so without this a filter typed before closing
/// the window would still hide rows on the next open and make them look lost. <see cref="Follow"/> watches the window's
/// visibility (closing hides it, <c>MainWindow.OnClosing</c>) and <see cref="ClearSearches"/> makes one call per view model,
/// no event bus: the two Commands tabs, the two Exclusions tabs, Gestures, Diagnostics › Log and › Recognition. A view model
/// not registered (a test, a tool) is skipped; one registered but not made yet is made, as showing its tab would.
/// </summary>
public static class HiddenWindowReset
{
    public static void Follow(Window window, IServiceProvider services)
    {
        ArgumentNullException.ThrowIfNull(window);
        ArgumentNullException.ThrowIfNull(services);
        window.PropertyChanged += (_, e) =>
        {
            if (e.Property == Visual.IsVisibleProperty && !window.IsVisible)
            {
                ClearSearches(services);
            }
        };
    }

    /// <summary>Empties every list's search box now (UI thread).</summary>
    public static void ClearSearches(IServiceProvider services)
    {
        ArgumentNullException.ThrowIfNull(services);
        foreach (var scope in new[] { CommandsScope.Global, CommandsScope.Apps })
        {
            if (services.GetKeyedService<CommandsViewModel>(scope) is { } commands)
            {
                commands.SearchText = string.Empty;
            }
        }

        foreach (var scope in new[] { IgnoreScope.Global, IgnoreScope.PerCommand, IgnoreScope.WhileHolding })
        {
            if (services.GetKeyedService<IgnoredViewModel>(scope) is { } ignored)
            {
                ignored.SearchText = string.Empty;
            }
        }

        if (services.GetService<GesturesViewModel>() is { } gestures)
        {
            gestures.SearchText = string.Empty;
        }

        if (services.GetService<LogViewModel>() is { } log)
        {
            log.SearchText = string.Empty;
        }

        if (services.GetService<RecognitionViewModel>() is { } recognition)
        {
            recognition.SearchText = string.Empty;
        }
    }
}
