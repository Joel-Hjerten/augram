# DevGallery

Debug-only (every file is `#if DEBUG`): the Gallery tab, one page per component, or per state of a component that has several, over fake data (the Gallery rule, App README "Rules of the road": a component is not done until it is here). `--gallery` opens it at start, and `tools/UiShots --gallery` renders every page; a page shows the dark and the light theme side by side (`ThemePair`).

## Adding a page

1. Write a static method returning a `ScreenDeclaration` (usually a `FormScreen` or a `ComponentScreen`) in the file for its area (`GestureGalleryPages`, `CommandGalleryPages`, …), or in a `<Name>GalleryPage` file of its own for a page with several parts.
2. Add one `new NavEntry("Title", Key + ".key", Method)` to `GalleryNavigation.Pages`, in the order the tab should list it. `Entry()` wraps every page in `ThemePair`; tests render `Pages` as declared, one theme each.
3. Fake data goes in `GalleryFakes` or the area's fakes (`CommandGalleryFakes`); a page never reads or writes the real config (`ShapeCleanupGalleryPage` only reads it).
4. Check it with `tools/UiShots --gallery` ([its README](../../../tools/UiShots/README.md)).

## Why the gallery keeps its own fakes

The tests share one set of fakes in `tests/Augram.TestSupport` (plan 0007, S16); the gallery does not use it, on purpose:

- The gallery is `#if DEBUG` code in the shipping App, and the App must never reference test code.
- Its fakes only answer, at once (a presenter that returns a fixed choice, a display list that changes nothing); the test fakes record calls and are scripted per test.
- Its presenter fakes fake App ports (form dialog, gesture picker, confirmation), which `Augram.TestSupport`, Core-only, cannot hold.

Where a gallery fake mirrors a test fake, its doc comment names that test fake: see [CommandGalleryFakes.cs](CommandGalleryFakes.cs) and [GalleryDisplayModes.cs](GalleryDisplayModes.cs). Keep the two in step when a port changes.

## What lives here

| File | Holds |
|---|---|
| `GalleryNavigation` | The Gallery tab and its pages. The SectionForm page has every field kind, among them a `CheckListField`, a `LinksField`, a `SliderField` (and one the Toggle disables through `Enabled`) and two colour fields with the rainbow presets (a preset selected, and a custom colour). |
| `GalleryFakes`, `GalleryDisplayModes` | The fake data (a pretend 4K TV for the Display mode form). |
| `GestureGalleryPages` | The Gestures-tab pages: glyph, grid, draw area, training content, import content. |
| `ShapeCleanupGalleryPage` | The shape cleanup preview: draw a stroke and see its cleaned shape over it, and this machine's gestures as drawn and cleaned (reads the configuration file only). |
| `CommandGalleryFakes`, `CommandGalleryPages` | The Commands-tab pages over a fake mapping with Joel's Blender and its Space hold remap: the Global and Apps workbenches, Hold remaps (Space open, Orbit selected, its Input and Remap step), Drag distance and Not in (`MappingWithZoom`: four live headers and the Not in dialog), the step list, every step form, the type picker in its refusal states (from Core's `StepOffer`), the gesture picker, the form dialog with the hold remap form. |
| `TriggerConflictGalleryPage` | Three live headers with a draft waiting: Take it and Swap in the note, and a note without them. |
| `ButtonTriggerGalleryPage` | Plan 0005 over `CommandGalleryFakes.MappingWithMagnifier`: the Global workbench with Magnifier (Right + Left) selected, the "Button trigger states" headers, the Also in dialog. |
| `SearchGalleryPages` | The search on the lists (plan 0009): the `SearchBox` in its states (empty, with text, own placeholder, focused), and each list with a search active (the ItemList page, Commands Apps on "zoom", Commands Global with no match, the gesture grid on "down", Exclusions on "blend", the gesture picker on "do" and with no match), over the areas' own fakes with their pages' `search` parameter. |
| `IgnoredGalleryPages` | Exclusions › Global over Joel's imported excluded apps, Per command with its "Used by" link, Global with "Allowed for", the bare `MasterDetail`, the `WindowFinder` and the identification form after a pretend pick. |
| `SyncGalleryPages` | The sync join question (one and two machines) and the conflict list (every kind, and empty). |
| `TransferGalleryPages` | The export dialog in each scope and the import review over a changed copy of the configuration (`ChangedCopy`), and over a file with nothing to import. |
| `CaptureGalleryPage` | The capture fields over fake ports: the hotkey field idle, capturing with and without the engine, refusing a modifier in one-key mode, ended by the engine; the button detector. |
| `ThemePair` | Shows a page's content in both theme variants side by side. |
