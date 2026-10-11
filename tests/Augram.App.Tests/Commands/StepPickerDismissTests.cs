using Augram.App.Components.StepList;
using Augram.Core.Abstractions;
using Augram.Core.Mapping;
using Augram.Core.Steps;
using Augram.Core.Steps.Delay;
using Augram.Core.Steps.WindowOp;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.LogicalTree;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Xunit;

namespace Augram.App.Tests.Commands;

/// <summary>
/// The step menu's New step… and Change to… open their pickers in flyouts that a click elsewhere closes, as the "New step…"
/// button's does (Joel, 0.11.6: only Escape closed Change to…, and nothing closed New step… opened from the menu).
/// </summary>
public sealed class StepPickerDismissTests
{
    [AvaloniaTheory]
    [InlineData(StepListAction.Add)]
    [InlineData(StepListAction.ChangeType)]
    public void APickerOpenedFromTheMenuClosesOnAClickElsewhere(StepListAction action)
    {
        var list = Show();
        var box = list.GetVisualDescendants().OfType<ListBox>().Single();
        var menu = box.ContextMenu!;
        var entry = menu.Items.OfType<MenuItem>().Single(item => Equals(item.Tag, action));

        // As a click on the entry does: the entry's Click while the menu is open, then the menu closes.
        box.RaiseEvent(new ContextRequestedEventArgs());
        entry.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(MenuItem.ClickEvent));
        menu.Close();
        Dispatcher.UIThread.RunJobs();

        var picker = action == StepListAction.Add ? list.TypePicker : list.ChangeTypePicker;
        var popup = picker.GetLogicalAncestors().OfType<Popup>().First();
        Assert.True(popup.IsOpen);

        var window = (Window)TopLevel.GetTopLevel(list)!;
        Dispatcher.UIThread.RunJobs();
        AvaloniaHeadlessPlatform.ForceRenderTimerTick();
        var outside = new Point(window.Bounds.Width - 5, window.Bounds.Height - 5);
        window.MouseDown(outside, MouseButton.Left);
        window.MouseUp(outside, MouseButton.Left);
        Dispatcher.UIThread.RunJobs();

        Assert.False(popup.IsOpen);
    }

    private static StepList Show()
    {
        IReadOnlyList<CommandStep> steps =
        [
            new(new WindowOpStep(WindowOperation.Center), HostPlatform.Windows),
            new(new DelayStep(50), HostPlatform.Windows),
        ];
        var list = new StepList { Steps = steps.Select((step, index) => StepItem.From(step, index, HostPlatform.Windows)).ToList(), SelectedIndex = 1, StepTypes = StepRegistry.BuiltIn.All, HasCommand = true };
        var window = new Window { Content = list, Width = 700, Height = 700 };
        window.Show();
        return list;
    }
}
