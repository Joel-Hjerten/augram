#if DEBUG
using Augram.App.Components.FormDialog;
using Augram.App.Declarations;
using Augram.App.Transfer;
using Augram.App.ViewModels;
using Augram.Core.Abstractions;
using Augram.Core.Config;
using Augram.Core.Diagnostics;
using Augram.Core.Gestures;
using Augram.Core.Mapping;
using Augram.Core.Steps.Delay;
using Augram.Core.Transfer;

namespace Augram.App.DevGallery;

/// <summary>
/// The Transfer page (plan 0003): the export dialog in each scope over the <see cref="CommandGalleryFakes"/> configuration
/// (everything and Chrome carry an imported placeholder step, so they show the typed-text note; gestures only and Blender do
/// not; an empty selection disables Save…), and the import review over a throwaway session of that configuration: a changed
/// copy of it (<see cref="ChangedCopy"/>: new, the same, different, matched by name and by shape, a repair, options) and the
/// file it would export itself (nothing to import).
/// </summary>
public static class TransferGalleryPages
{
    public const double DialogWidth = 560;
    public const double ReviewWidth = 960;
    public const double ReviewHeight = 640;

    public static ScreenDeclaration Page()
    {
        var current = Configuration();
        var chrome = current.Mapping.Groups.Single(group => group.Name == "Chrome").Id;
        var blender = current.Mapping.Groups.Single(group => group.Name == "Blender").Id;
        return new FormScreen("Transfer",
        [
            new Section("Export: everything (Options › Configuration), with the typed-text note",
            [
                new CustomField("Export", () => ExportDialog(current, ExportScope.Everything)),
            ]),
            new Section("Export: gestures only (the Gestures toolbar), no note",
            [
                new CustomField("Export", () => ExportDialog(current, ExportScope.GesturesOnly)),
            ]),
            new Section("Export: Blender selected (its menu in Commands › Apps), no note",
            [
                new CustomField("Export", () => ExportDialog(current, ExportScope.Of([blender]))),
            ]),
            new Section("Export: Chrome selected, with the note",
            [
                new CustomField("Export", () => ExportDialog(current, ExportScope.Of([chrome]))),
            ]),
            new Section("Export: nothing selected yet (Save… disabled)",
            [
                new CustomField("Export", () => ExportDialog(current, ExportScope.Of([]))),
            ]),
            new Section("Import review: a changed copy (new Firefox, Steam games matched by name, North matched by shape to Up, Z and Close tab different, Back unbound, options)",
            [
                new CustomField("Import", () => Review(current, Exporter.Export(ExportScope.Everything, ChangedCopy(current)), "Augram everything 2026-10-10.augram.json")),
            ]),
            new Section("Import review: the file this configuration exports (nothing to import)",
            [
                new CustomField("Import", () => Review(current, Exporter.Export(ExportScope.Of([blender]), current), "Augram Blender 2026-10-10.augram.json")),
            ]),
        ]);
    }

    /// <summary>The Commands gallery's mapping over the starter gestures with default options: the configuration the pages export.</summary>
    public static ConfigDocument Configuration() => new()
    {
        Settings = Settings.Default,
        Gestures = StarterGestures.All(),
        Mapping = MappingRules.ValidDocument(CommandGalleryFakes.Mapping()),
    };

    /// <summary>
    /// The configuration as another machine has it: Up called North under a new id (matched by shape; its commands follow it),
    /// the starter Z drawn as S (different), Steam games under new ids (matched by name, the same), Chrome's Close tab moved
    /// to Down (different) and a new Back on North (unbound while Close tab keeps Up here), a new Firefox group, the stroke
    /// button Middle.
    /// </summary>
    public static ConfigDocument ChangedCopy(ConfigDocument current)
    {
        ArgumentNullException.ThrowIfNull(current);
        var up = StarterGestures.IdFor("Up");
        var north = current.Gestures.Single(gesture => gesture.Id == up) with { Id = GestureId.New(), Name = "North" };
        var toNorth = new Dictionary<GestureId, GestureId> { [up] = north.Id };
        var drawnAsS = current.Gestures.Single(gesture => gesture.Name == "S").Samples;
        Gesture[] gestures =
        [
            .. current.Gestures.Select(gesture => gesture.Id == up ? north : gesture.Name == "Z" ? gesture with { Samples = drawnAsS } : gesture),
        ];
        var firefox = new AppGroup(GroupId.New(), "Firefox", IsActive: true, SuppressGlobals: false, new AppMatcher { WindowsProcessNames = ["firefox.exe"] },
            [new Command(CommandId.New(), "Reload", Trigger.ForGesture(north.Id), IsActive: true, [new CommandStep(new DelayStep(20), HostPlatform.Windows)])]);
        var groups = current.Mapping.Groups
            .Select(group => group with { Commands = [.. group.Commands.Select(command => command.WithGesturesReplaced(toNorth))] })
            .Select(group => group.Name switch
            {
                "Steam games" => group with { Id = GroupId.New(), Commands = [.. group.Commands.Select(command => command with { Id = CommandId.New() })] },
                "Chrome" => group with
                {
                    Commands =
                    [
                        .. group.Commands.Select(command => command.Name == "Close tab" ? command with { Trigger = Trigger.ForGesture(StarterGestures.IdFor("Down")) } : command),
                        new Command(CommandId.New(), "Back", Trigger.ForGesture(north.Id), IsActive: true, [new CommandStep(new DelayStep(5), HostPlatform.Windows)]),
                    ],
                },
                _ => group,
            });
        return current with
        {
            Settings = current.Settings with { General = current.Settings.General with { StrokeButton = Core.Capture.MouseButton.Middle } },
            Gestures = gestures,
            Mapping = MappingRules.ValidDocument(current.Mapping with { Groups = [.. groups, firefox] }),
        };
    }

    private static FormDialog ExportDialog(ConfigDocument current, ExportScope start)
    {
        var dialog = FormDialogPresenter.Build(ExportPresenter.Request(new ExportViewModel(current, start)));
        dialog.Width = DialogWidth;
        return dialog;
    }

    /// <summary>The review over a throwaway session of <paramref name="current"/> (the same ids the file was made from): Import changes only that session.</summary>
    private static AugramImportView Review(ConfigDocument current, TransferFile file, string fileName)
    {
        var session = new ConfigSession(new GalleryConfigStore(current), _ => NoSave.Instance);
        return new AugramImportView(new AugramImportViewModel(session, file, fileName, NullEventLog.Instance)) { Width = ReviewWidth, Height = ReviewHeight };
    }

    /// <summary>A configuration in memory that never saves.</summary>
    private sealed class GalleryConfigStore : IConfigStore
    {
        private readonly ConfigDocument _document;

        public GalleryConfigStore(ConfigDocument document)
        {
            _document = document;
        }

        public string Location => "gallery://augram.json";

        public ConfigDocument Load() => _document;

        public void Save(ConfigDocument document)
        {
        }
    }

    private sealed class NoSave : IDisposable
    {
        public static NoSave Instance { get; } = new();

        public void Dispose()
        {
        }
    }
}
#endif
