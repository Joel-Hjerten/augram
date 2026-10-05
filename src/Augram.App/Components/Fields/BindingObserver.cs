using Augram.App.Declarations;
using Avalonia.Controls;
using Avalonia.Threading;

namespace Augram.App.Components.Fields;

/// <summary>
/// Keeps an editor control in step with its binding: applies the current value when the control
/// enters the visual tree, re-applies on every <see cref="IValueBinding.Changed"/> (marshalled to the UI
/// thread when raised elsewhere), and unsubscribes when the control leaves the tree, so a hidden tab
/// holds no live subscription.
/// </summary>
public static class BindingObserver
{
    public static void Attach<T>(Control editor, IValueBinding<T> binding, Action<T> apply)
    {
        ArgumentNullException.ThrowIfNull(editor);
        ArgumentNullException.ThrowIfNull(binding);
        ArgumentNullException.ThrowIfNull(apply);
        Attach(editor, binding, () => apply(binding.Get()));
    }

    public static void Attach(Control editor, IValueBinding binding, Action apply)
    {
        ArgumentNullException.ThrowIfNull(editor);
        ArgumentNullException.ThrowIfNull(binding);
        ArgumentNullException.ThrowIfNull(apply);

        void OnChanged(object? sender, EventArgs e)
        {
            if (Dispatcher.UIThread.CheckAccess())
            {
                apply();
            }
            else
            {
                Dispatcher.UIThread.Post(apply);
            }
        }

        apply();
        editor.AttachedToVisualTree += (_, _) =>
        {
            binding.Changed += OnChanged;
            apply();
        };
        editor.DetachedFromVisualTree += (_, _) => binding.Changed -= OnChanged;
    }
}
