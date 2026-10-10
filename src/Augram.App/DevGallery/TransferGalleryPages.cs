#if DEBUG
using Augram.App.Components.FormDialog;
using Augram.App.Declarations;
using Augram.App.Transfer;
using Augram.App.ViewModels;
using Augram.Core.Config;
using Augram.Core.Gestures;
using Augram.Core.Mapping;
using Augram.Core.Transfer;

namespace Augram.App.DevGallery;

/// <summary>
/// The Transfer page (plan 0003): the export dialog in each scope over the <see cref="CommandGalleryFakes"/> configuration
/// (everything and Chrome carry an imported placeholder step, so they show the typed-text note; gestures only and Blender do
/// not; an empty selection disables Save…).
/// </summary>
public static class TransferGalleryPages
{
    public const double DialogWidth = 560;

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
        ]);
    }

    /// <summary>The Commands gallery's mapping over the starter gestures with default options: the configuration the pages export.</summary>
    public static ConfigDocument Configuration() => new()
    {
        Settings = Settings.Default,
        Gestures = StarterGestures.All(),
        Mapping = MappingRules.ValidDocument(CommandGalleryFakes.Mapping()),
    };

    private static FormDialog ExportDialog(ConfigDocument current, ExportScope start)
    {
        var dialog = FormDialogPresenter.Build(ExportPresenter.Request(new ExportViewModel(current, start)));
        dialog.Width = DialogWidth;
        return dialog;
    }
}
#endif
