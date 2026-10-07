using Augram.App.Components.Fields;
using Augram.App.Declarations;
using Augram.App.ViewModels;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;

namespace Augram.App.Screens;

/// <summary>
/// The Sync section of the Options tab (F8 sync), declared like the rest of the page: repository address, this
/// machine's name, automatic sync, Sync now with the status line, the conflicts line with Resolve… (shown only while
/// conflicts are pending) and the last run's details. The two text fields commit on leaving or Enter, not per
/// keystroke (<see cref="SyncViewModel"/>), so they are custom editors: a text box with its problem shown under it.
/// </summary>
public static class OptionsSyncSection
{
    public const string Title = "Sync";

    public const string RepositoryHelp =
        "A private GitHub repository, e.g. https://github.com/you/augram-settings. Sign-in is git's own (Git Credential Manager on Windows, the Keychain on macOS); Augram stores no password. Clear it to turn sync off.";

    public const string SectionHelp =
        "Gestures and commands follow you between machines through a private git repository; everything else on this page stays on each machine.";

    public static Section Declare(SyncViewModel vm)
    {
        ArgumentNullException.ThrowIfNull(vm);
        return new Section(Title,
        [
            new CustomField("Repository", () => CommittedText(
                new DelegateBinding<string>(() => vm.RepositoryUrlText, value => vm.RepositoryUrlText = value, vm),
                new DelegateBinding<string>(() => vm.UrlProblemText, owner: vm),
                vm.CommitRepositoryUrl,
                vm.RevertRepositoryUrl,
                wide: true), vm, RepositoryHelp),
            new CustomField("This machine", () => CommittedText(
                new DelegateBinding<string>(() => vm.MachineNameText, value => vm.MachineNameText = value, vm),
                new DelegateBinding<string>(() => vm.MachineNameProblemText, owner: vm),
                vm.CommitMachineName,
                vm.RevertMachineName,
                wide: false), vm, "How the other machines name this one (\"3 commands changed from PC-HOME\")."),
            new ToggleField("Automatic sync",
                new DelegateBinding<bool>(() => vm.AutoSync, value => vm.AutoSync = value, vm),
                "At start, 20 seconds after a change and every 5 minutes. Off: only Sync now."),
            new CustomField("Status", () => StatusEditor(vm), vm),
            new CustomField("Conflicts", () => ConflictsEditor(vm), vm, "Changed here and on another machine. Until you choose, this machine keeps its version; nothing blocks.")
            {
                Visible = new DelegateBinding<bool>(() => vm.HasConflicts, owner: vm, propertyName: nameof(vm.HasConflicts)),
            },
            new NoteField("Details", new DelegateBinding<string>(() => vm.DetailsText, owner: vm), "What the last sync renamed or skipped.")
            {
                Visible = new DelegateBinding<bool>(() => vm.HasDetails, owner: vm, propertyName: nameof(vm.HasDetails)),
            },
        ], SectionHelp);
    }

    /// <summary>A text box that reports every keystroke as a draft and commits on leaving or Enter (Escape reverts), with the draft's problem under it.</summary>
    internal static Control CommittedText(IValueBinding<string> text, IValueBinding<string> problem, Action commit, Action revert, bool wide)
    {
        var editor = new TextBox();
        editor.Classes.Add("field-editor");
        if (wide)
        {
            editor.Classes.Add("wide");
        }

        BindingObserver.Attach(editor, text, value =>
        {
            if (editor.Text != value)
            {
                editor.Text = value;
            }
        });
        editor.PropertyChanged += (_, e) =>
        {
            if (e.Property == TextBox.TextProperty)
            {
                text.Set(editor.Text ?? string.Empty);
            }
        };
        editor.LostFocus += (_, _) => commit();
        editor.AddHandler(InputElement.KeyDownEvent, (_, e) =>
        {
            if (e.Key == Key.Enter)
            {
                commit();
                e.Handled = true;
            }
            else if (e.Key == Key.Escape)
            {
                revert();
                e.Handled = true;
            }
        }, RoutingStrategies.Tunnel);

        var error = new TextBlock();
        error.Classes.Add("error");
        BindingObserver.Attach(error, problem, value =>
        {
            error.Text = value;
            error.IsVisible = value.Length > 0;
        });
        return new StackPanel { Children = { editor, error } };
    }

    /// <summary>Sync now and the status line beside it.</summary>
    private static Control StatusEditor(SyncViewModel vm)
    {
        var button = ActionButton("Sync now", vm.SyncNow, new DelegateBinding<bool>(() => vm.CanSyncNow, owner: vm, propertyName: nameof(vm.CanSyncNow)));
        var status = new TextBlock { VerticalAlignment = VerticalAlignment.Center, TextWrapping = Avalonia.Media.TextWrapping.Wrap };
        status.Classes.Add("note");
        BindingObserver.Attach(status, new DelegateBinding<string>(() => vm.StatusText, owner: vm, propertyName: nameof(vm.StatusText)), value => status.Text = value);
        return Line(button, status);
    }

    /// <summary>"2 conflicts with Mac" and Resolve….</summary>
    private static Control ConflictsEditor(SyncViewModel vm)
    {
        var button = ActionButton("Resolve…", () => _ = vm.ResolveConflictsAsync(), new DelegateBinding<bool>(() => vm.HasConflicts, owner: vm, propertyName: nameof(vm.HasConflicts)));
        var text = new TextBlock { VerticalAlignment = VerticalAlignment.Center, TextWrapping = Avalonia.Media.TextWrapping.Wrap };
        text.Classes.Add("note");
        BindingObserver.Attach(text, new DelegateBinding<string>(() => vm.ConflictsText, owner: vm, propertyName: nameof(vm.ConflictsText)), value => text.Text = value);
        return Line(button, text);
    }

    private static DockPanel Line(Button button, TextBlock text)
    {
        DockPanel.SetDock(button, Dock.Left);
        var line = new DockPanel { Children = { button, text } };
        line.Classes.Add("field-line");
        return line;
    }

    private static Button ActionButton(string label, Action click, IValueBinding<bool> enabled)
    {
        var button = new Button { Content = label };
        button.Classes.Add("toolbar");
        button.Click += (_, _) => click();
        BindingObserver.Attach(button, enabled, value => button.IsEnabled = value);
        return button;
    }
}
