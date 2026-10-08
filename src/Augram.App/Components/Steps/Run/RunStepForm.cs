using Augram.App.Components.Fields;
using Augram.App.Declarations;
using Augram.Core.Steps;
using Augram.Core.Steps.Run;
using Avalonia.Controls;
using Avalonia.Platform.Storage;

namespace Augram.App.Components.Steps.Run;

/// <summary>
/// The Run step's form (F5): Program (a text field that takes a path, a bare name such as <c>explorer</c> or a link such as
/// <c>ms-settings:display</c>, with Browse… beside it on the line the Options › Sync rows use), Arguments, Start in, "Run as
/// administrator" (Windows only: macOS has no UAC and the launcher declines it there) and "Hidden window". Every edit emits
/// one new <see cref="RunStep"/>; a browsed file replaces the text and emits once. The sections are a declared
/// <see cref="FormScreen"/> (ADR-0002 §5c); the Program line is a custom field built from the Text renderer and a toolbar button.
/// </summary>
public sealed class RunStepForm : IStepForm
{
    public const string BrowseLabel = "Browse…";

    public const string ProgramHelp = "A program, document, folder or link: a path, a name such as explorer, or ms-settings:display. %VAR% is expanded.";

    public const string ElevatedLabel = "Run as administrator";

    public const string HiddenLabel = "Hidden window";

    private static readonly FilePickerFileType Programs = new("Programs")
    {
        Patterns = ["*.exe", "*.bat", "*.cmd", "*.com", "*.lnk", "*.msc"],
        AppleUniformTypeIdentifiers = ["com.apple.application"],
    };

    /// <summary>"Run as administrator" is offered on Windows only.</summary>
    public static bool OffersElevation => OperatingSystem.IsWindows();

    public string TypeKey => RunStepType.Instance.Key;

    public Control Build(IStep current, Action<IStep> changed)
    {
        ArgumentNullException.ThrowIfNull(changed);
        var state = StepParameters.Expect<RunStep>(current, RunStepType.Instance);
        void Emit(RunStep next)
        {
            if (next == state)
            {
                return;
            }

            state = next;
            changed(next);
        }

        var file = new DelegateBinding<string>(() => state.File, value => Emit(state with { File = value }), propertyName: nameof(RunStep.File));
        var fields = new List<Field>
        {
            new CustomField("Program", () => ProgramLine(file), Help: ProgramHelp),
            new TextField(
                "Arguments",
                new DelegateBinding<string>(() => state.Arguments, value => Emit(state with { Arguments = value })),
                "Everything after the program on a command line: /f /im yuzu.exe. Not written to the log."),
            new TextField(
                "Start in",
                new DelegateBinding<string>(() => state.WorkingDirectory, value => Emit(state with { WorkingDirectory = value })),
                "Optional folder the program starts in; empty is your home folder."),
        };
        if (OffersElevation)
        {
            fields.Add(new ToggleField(
                ElevatedLabel,
                new DelegateBinding<bool>(() => state.Elevated, value => Emit(state with { Elevated = value })),
                "Asks for administrator rights (the UAC prompt) every time it runs; declining skips the step."));
        }

        fields.Add(new ToggleField(
            HiddenLabel,
            new DelegateBinding<bool>(() => state.Hidden, value => Emit(state with { Hidden = value })),
            OffersElevation ? "Starts without a window: for console tools such as taskkill." : "Starts the app hidden, in the background."));
        return new SectionForm.SectionForm { Screen = new FormScreen("Run", [new Section("Run", fields)]) };
    }

    /// <summary>Browse… then the text field, on one line.</summary>
    private static DockPanel ProgramLine(DelegateBinding<string> file)
    {
        var browse = new Button { Content = BrowseLabel };
        browse.Classes.Add("toolbar");
        browse.Click += (_, _) => _ = BrowseAsync(browse, file);
        DockPanel.SetDock(browse, Dock.Left);
        var line = new DockPanel { Children = { browse, FieldRendererRegistry.Default.Build(new TextField("Program", file)) } };
        line.Classes.Add("field-line");
        return line;
    }

    private static async Task BrowseAsync(Control anchor, DelegateBinding<string> file)
    {
        if (TopLevel.GetTopLevel(anchor)?.StorageProvider is not { CanOpen: true } storage)
        {
            return;
        }

        var picked = await storage.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Choose a program or document to run",
            AllowMultiple = false,
            FileTypeFilter = [Programs, FilePickerFileTypes.All],
        }).ConfigureAwait(true);
        if (picked.Count > 0 && picked[0].TryGetLocalPath() is { } path)
        {
            file.Set(path);
            file.NotifyChanged();
        }
    }
}
