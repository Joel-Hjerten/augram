using System.Reflection;
using System.Text.RegularExpressions;
using Augram.App.Components.CommandHeader;
using Augram.App.Components.CommandTree;
using Augram.App.Components.GestureGrid;
using Xunit;
using static Augram.App.Tests.Architecture.SourceScan;

namespace Augram.App.Tests.Architecture;

/// <summary>
/// One home per list and component mechanism (plan 0007 S14, S14b; docs/reference/one-home-guards.md): the edit keys, menus,
/// rename in place, asking dropdowns, template parts, toolbar buttons, step form state, hotkey choices, the header's own
/// fields, item projections, and a list's selection, focus and clicks (RowListHost).
/// </summary>
public sealed partial class OneHomeComponentTests
{
    private const string Components = "src/Augram.App/Components/";

    /// <summary>The list components (their folders under Components/): what RowListHost serves.</summary>
    private static readonly string[] ListControls = ["CommandTree", "StepList", "GestureGrid", "MasterDetail", "ItemList", "SyncConflictList", "ListEditing"];

    private static IEnumerable<string> AppCode => Code("src/Augram.App");

    [Fact]
    public void EditKeysComeFromEditKeymap()
        => AssertOnlyIn(
            EditKey(),
            AppCode,
            "Use Components/ListEditing/EditKeymap (Current.Rename, RenameAlso, Undo, …) for an edit key or its name (plan 0007 S14).",
            new Allowed(Components + "ListEditing/EditKeymap.cs", "the home"));

    [Fact]
    public void MenusAndKeyBindingsAreBuiltByActionMenu()
        => AssertOnlyIn(
            MenuOrBinding(),
            Code(Components),
            "Use Components/ListEditing/ActionMenu<TAction> (a list's …Menu table) instead of a MenuItem or KeyBinding of its own (plan 0007 S14).",
            new Allowed(Components + "ListEditing/ActionMenu.cs", "the home"));

    [Fact]
    public void OnlyItemRowFindsItsNameEditor()
        => AssertOnlyIn(
            NameEditor(),
            AppCode,
            "Rename in place through Components/ListEditing/ItemRow (BeginRename, RenameCommitted) (plan 0007 S14).",
            new Allowed(Components + "ListEditing/ItemRow.cs", "the home"));

    [Fact]
    public void TemplateButtonsAreWiredByTemplateParts()
        => AssertOnlyIn(
            FindButton(),
            AppCode,
            "Use Components/Parts/TemplateParts.OnClick to wire a template's button (plan 0007 S14).",
            new Allowed(Components + "Parts/TemplateParts.cs", "the home"));

    [Fact]
    public void ToolbarButtonsAreMadeByToolbarButton()
        => AssertOnlyIn(
            ToolbarClass(),
            AppCode,
            "Use Components/Parts/ToolbarButton.Create (plan 0007 S14).",
            new Allowed(Components + "Parts/ToolbarButton.cs", "the home"));

    [Fact]
    public void StepFormsKeepTheirStateInStepFormState()
        => AssertOnlyIn(
            OwnEmit(),
            Code(Components + "Steps"),
            "Use Components/Steps/StepFormState<TStep> (Emit, the current step) instead of a form's own (plan 0007 S14).",
            new Allowed(Components + "Steps/StepFormState.cs", "the home"),
            new Allowed(Components + "Steps/Remap/RemapStepForm.cs", "forwards to its StepFormState", "_form.Emit"),
            new Allowed(Components + "Steps/Hdr/HdrStepForm.cs", "leftover, fold later: S14's watch list keeps its own state"),
            new Allowed(Components + "Steps/WindowOp/WindowOpStepForm.cs", "leftover, fold later: S14's watch list keeps its own state"),
            new Allowed(Components + "Steps/DisplayMode/DisplayModeStepForm.cs", "leftover, fold later: S14's watch list keeps its own state"),
            new Allowed(Components + "Steps/OpenApp/OpenAppStepForm.cs", "leftover, fold later: S14's watch list keeps its own state"));

    [Fact]
    public void HotkeyChoicesAreBuiltByHotkeyChoices()
        => AssertOnlyIn(
            HotkeyChoice(),
            AppCode,
            "Use Components/Capture/HotkeyChoices (the modifier toggles and the key list) (plan 0007 S14).",
            new Allowed(Components + "Capture/HotkeyChoices.cs", "the home"));

    [Fact]
    public void HeaderOnlyFieldsLiveOnCommandHeaderItem()
    {
        // S14: what only the command header shows is on CommandHeaderItem; the tree's CommandItem stays the row.
        string[] headerOnly = ["StepRefusals", "ChangeRefusals", "DraftNote", "ConflictName", "CanSwapTrigger", "CanTakeTrigger", "OptionsDragDistancePx", "StrokeButton"];
        var onRow = typeof(CommandItem).GetMembers(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly)
            .Select(member => member.Name).Intersect(headerOnly).ToList();

        Assert.True(onRow.Count == 0, $"Header-only members on CommandItem: {string.Join(", ", onRow)}. Put them on Components/CommandHeader/CommandHeaderItem (plan 0007 S14).");
        Assert.All(headerOnly, name => Assert.NotNull(typeof(CommandHeaderItem).GetProperty(name)));
    }

    [Fact]
    public void ComponentItemsAreProjectedOutsideTheComponents()
    {
        // S14: view models project their items (ViewModels/Commands/Projection); a Components item record has no static From.
        var offenders = typeof(CommandItem).Assembly.GetTypes()
            .Where(type => type.Namespace?.StartsWith("Augram.App.Components", StringComparison.Ordinal) == true && IsRecord(type))
            .Where(type => type.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.DeclaredOnly).Any(method => method.Name == "From"))
            .Where(type => type != typeof(GestureTileItem))
            .Select(type => type.FullName)
            .ToList();

        Assert.True(offenders.Count == 0, $"Components item records with a static From: {string.Join(", ", offenders)}. Project them in ViewModels/Commands/Projection (CommandItemProjection, StepItemProjection) instead (plan 0007 S14); GestureTileItem.From is the one kept.");
    }

    [Fact]
    public void ListSelectionFocusAndClicksLiveInRowListHost()
        => AssertOnlyIn(
            ListMechanics(),
            Code(ListControls.Select(folder => Components + folder).ToArray()),
            "Use Components/ListEditing/RowListHost (a field of the list: selection by id without an echo, rebuild with focus, right-click select, the double-click rule, keys standing down while editing) (plan 0007 S14b).",
            new Allowed(Components + "ListEditing/RowListHost.cs", "the home"),
            new Allowed(Components + "ListEditing/ItemRow.cs", "the row's own double click starts its rename", "ClickCount"),
            new Allowed(Components + "ItemList/ListRowBuilder.cs", "a row's ComboBox, not the list's selection", "combo.SelectionChanged"));

    private static bool IsRecord(Type type) => type.GetMethod("<Clone>$") is not null;

    // F2, Return, Enter as key-gesture strings or a Ctrl+/Cmd+ gesture; a rename bound to Key.F2.
    [GeneratedRegex(@"""(?:F2|Return|Enter)""|""(?:Ctrl|Cmd)\+|\bKey\.F2\b")]
    private static partial Regex EditKey();

    [GeneratedRegex(@"\bnew\s+(?:MenuItem|KeyBinding)\b")]
    private static partial Regex MenuOrBinding();

    [GeneratedRegex(@"PART_NameEditor")]
    private static partial Regex NameEditor();

    [GeneratedRegex(@"\bNameScope\.Find<Button>\(")]
    private static partial Regex FindButton();

    [GeneratedRegex(@"Classes\.Add\(\s*""toolbar""\s*\)")]
    private static partial Regex ToolbarClass();

    [GeneratedRegex(@"\bvoid\s+Emit\s*\(")]
    private static partial Regex OwnEmit();

    [GeneratedRegex(@"\bnew\s+ModifierToggle\s*\(\s*\)|\bEnum\.GetValues<KeyCode>\(\)")]
    private static partial Regex HotkeyChoice();

    // A list doing RowListHost's job: its own selection or double-tap handler, focusing a container, a right press selecting,
    // an echo flag, the double-click rule, a ListBox field, writing SelectedItem, or "keys stand down while editing".
    [GeneratedRegex(@"\.SelectionChanged\s*\+=|\.DoubleTapped\s*\+=|ContainerFromItem\([^)]*\)\??\.Focus\(|\bIsRightButtonPressed\b|\bbool\s+_applying\b|\bClickCount\s*==\s*2\b|\bListBox\s+_\w+\s*[;=]|\.SelectedItem\s*=(?!=)|\(\)\s*=>\s*!IsEditing\b")]
    private static partial Regex ListMechanics();
}
