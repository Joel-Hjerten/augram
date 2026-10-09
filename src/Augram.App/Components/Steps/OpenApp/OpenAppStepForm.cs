using Augram.App.Components.WindowFinder;
using Augram.App.Declarations;
using Augram.App.Hosting;
using Augram.Core.Abstractions;
using Augram.Core.Steps;
using Augram.Core.Steps.OpenApp;
using Avalonia.Controls;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Augram.App.Components.Steps.OpenApp;

/// <summary>
/// The Open app step's form (Joel, 2026-10-09): "This app (Augram)" (opens Augram's own window, and hides the rest), then
/// the app per platform, as an app group names its executables: Windows ("chrome.exe") and macOS ("Google Chrome"), each
/// with the magnifier on this machine's platform, and a line saying what opens here (the known-app guess for an empty
/// platform). Every edit emits one new <see cref="OpenAppStep"/>.
/// </summary>
public sealed partial class OpenAppStepForm : IStepForm
{
    public const string AugramLabel = "This app (Augram)";

    public string TypeKey => OpenAppStepType.Instance.Key;

    public Control Build(IStep current, Action<IStep> changed)
    {
        ArgumentNullException.ThrowIfNull(changed);
        var state = new FormState(StepParameters.Expect<OpenAppStep>(current, OpenAppStepType.Instance), changed);
        var platform = CommandsModule.CurrentPlatform;
        var apps = new DelegateBinding<bool>(() => !state.Step.IsAugram, owner: state, propertyName: null);
        var fields = new List<Field>
        {
            new ToggleField(AugramLabel, new DelegateBinding<bool>(() => state.Step.IsAugram, value => state.Emit(state.Step with { IsAugram = value }), state, propertyName: null),
                "Opens Augram's own window, as a double click on its tray or menu-bar icon does."),
            new TextField("Windows app", new DelegateBinding<string>(() => state.Step.WindowsApp, value => state.Emit(state.Step with { WindowsApp = value }), state, propertyName: null),
                "The executable's file name: chrome.exe. Brought to the front when it has a window open, started otherwise.")
            {
                Visible = apps,
                Accessory = platform == HostPlatform.Windows ? WindowFinderAccessory.Finder(window => window.ProcessName, window => state.Emit(state.Step with { WindowsApp = window.ProcessName })) : null,
            },
            new TextField("macOS app", new DelegateBinding<string>(() => state.Step.MacApp, value => state.Emit(state.Step with { MacApp = value }), state, propertyName: null),
                "The app's name: Google Chrome. Brought to the front when it runs, started otherwise.")
            {
                Visible = apps,
                Accessory = platform == HostPlatform.MacOS ? WindowFinderAccessory.Finder(window => window.ProcessName, window => state.Emit(state.Step with { MacApp = window.ProcessName })) : null,
            },
            new NoteField("Here it opens", new DelegateBinding<string>(() => Here(state.Step, platform), owner: state, propertyName: null),
                "An empty platform uses the well-known name of the other platform's app, when there is one."),
        };
        return new SectionForm.SectionForm { Screen = new FormScreen("Open app", [new Section("Open app", fields)]) };
    }

    private static string Here(OpenAppStep step, HostPlatform platform)
        => step.IsAugram ? "Augram's window" : step.AppFor(platform) ?? "nothing: name the app for this platform";

    /// <summary>The step being edited, observable so the form's bindings re-read after each edit.</summary>
    private sealed partial class FormState(OpenAppStep step, Action<IStep> changed) : ObservableObject
    {
        [ObservableProperty]
        public partial OpenAppStep Step { get; private set; } = step;

        public void Emit(OpenAppStep next)
        {
            if (next == Step)
            {
                return;
            }

            Step = next;
            changed(next);
        }
    }
}
