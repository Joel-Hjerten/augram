# Plan 0009: search and filter on the lists

Status: **MERGED as 0.13.0 (2026-10-12, PC session), awaiting Joel's acceptance check below.** Built by a worktree agent, reviewed and merged by the lead. Origin: Joel, 2026-10-12: "the ability to search and filter the lists. Both for the app groups and commands. And then a search function inside the gesture panel. … I just write something and it starts filtering out groups containing that. And commands." Exclusions too ("as they don't have sub items it's easier").

## Decisions (Joel, 2026-10-12)

1. **A search box on every list's toolbar**: the Gestures tab, the gesture picker, Commands › Global, Commands › Apps, Exclusions › Global and › Per command. The lists that are a `ListSpec` (Log, Recognition) get it through the declaration, free. One lookless component for the box, one home for the matching rule.
2. **Matching is case-insensitive "contains" on the name.** Space-separated words must all match somewhere in the name ("down left" finds "Down then left"). No fuzzy matching. Starts-with results sort first only in the gesture picker; the tabs keep their own order.
3. **Names only.** A command matches on its name; a group on its name; a gesture on its name; an excluded app on its name or its summary (the process pattern). Nothing inside steps (Joel: "no looking inside steps"), no trigger text, no gesture name on the Commands tab in this version (a fallback if Joel asks for "which command is on Down").
4. **The tree rule on the Commands tabs** (the convention of tree filters, chosen over strict hiding and over scope checkboxes): a section that matches by name shows with all its commands, expanded; a command that matches shows under its section, the section's other commands hidden, the section expanded; everything else is hidden. On the Apps tab "section" means both levels: the app group's name and the category's name, so a match on either shows everything under it. While the box is empty the saved expanded set rules as before. **Fallback if noise shows up** (a group name that is also in many command names): two checkboxes, Groups and Commands, as scope flags on the same rule; not built now.
5. **Selection survives the search.** A selected row hidden by the search keeps its side panel; clearing the box shows it again, still selected and expanded, so "find the group, clear, look inside" always works. No nudge (the platform filter's blink is for edits, not for searching).
6. **Keys and clearing.** Ctrl+F (Cmd+F on macOS) focuses the box from anywhere on the tab; Escape in the box clears it; a clear glyph in the box does the same. Typing filters at once (no Enter).
7. **Empty state**: "No commands match 'xyz'", "No gestures match 'xyz'", "No apps match 'xyz'", with the search text in the sentence, in the list's existing empty-state place.
8. **Not persisted.** The text is UI-only state (the State rule); it clears when the main window closes, so a hidden filter never makes rows look lost on the next open. The Commands and Exclusions view models are singletons, so this is an explicit clear on hide, not a side effect of disposal.

## What is built

| Piece | Home | Notes |
|---|---|---|
| The box | `Components/SearchBox/SearchBox` (new folder, README, ControlTheme in `Themes/Default/Forms.axaml`, gallery page) | `Text` (two-way), `Placeholder`, `PART_Text`, `PART_Clear`; Escape clears; `Focus()` for Ctrl+F. Tokens only (theme rule); the text role from `Text.axaml`. |
| The rule | `Components/SearchBox/SearchQuery` | `SearchQuery.Parse(text)` → words; `Matches(name)` (every word a case-insensitive substring); `IsEmpty`; `StartsWith(name)` for the picker's ordering. The one place a search compares text; nothing else calls `Contains(…, OrdinalIgnoreCase)` for a search. |
| `ListSpec` lists | `Declarations/ListSearch` (a `IValueBinding<string>` and a `Func<object, string>` naming the row's text), applied in `Components/ItemList/ItemList` beside the `ListFilter`s; `ListRowBuilder` puts the box after the dropdown filters | Log and Recognition declare one. |
| Commands › Global and › Apps | `CommandsViewModel.SearchText` (UI-only, like `ShowOtherPlatforms`), applied in `CommandSections.For` after the platform filter with decision 4's rule; the box on `CommandTree`'s toolbar beside the platform toggle, through `CommandsWorkbench` | The expanded set is not written while searching; the tree only renders the sections expanded. |
| Gestures tab | `GesturesViewModel.SearchText`, applied where `Tiles` is projected; the box on `GestureGrid`'s toolbar | Partners stay by id; a hidden partner is simply not lit. |
| Gesture picker | `GesturePicker` gets the box above the tiles (in the shell's content); `GesturePickerPresenter` applies the rule with the starts-with-first order | The picker's selection is kept by id across refilters. |
| Exclusions | `IgnoredViewModel.SearchText`, applied where `Items` is projected; the box on `MasterDetail`'s toolbar | Matches name or summary. |
| Clearing on hide | the one place the main window's hide is handled (`Hosting/`, see its README) tells the singletons to clear | One call per view model, no new event bus. |
| Guard | one row in [reference/one-home-guards.md](../reference/one-home-guards.md): a search compares text only in `SearchQuery`; a text box with a clear glyph is `SearchBox` | `OneHome…Tests`, a source scan for `OrdinalIgnoreCase` next to `Contains(` under `Components/` and `ViewModels/` with the allowlist. |

## Tests (one per behaviour, beside the neighbours)

- `SearchQueryTests`: words all match, case-insensitive, empty query matches everything, starts-with ordering.
- `CommandSectionsTests`: a section matching by name shows all its commands expanded; a command match shows only it under its expanded section; both levels on the Apps tab; the platform filter still applies first; an empty query leaves the saved expanded set in charge.
- `CommandsViewModelTests`: the selection survives a search that hides it and is back after clearing.
- `GesturesViewModelTests`: tiles filter by name; `GesturePickerPresenterTests` (or the picker's existing tests): starts-with first.
- `IgnoredViewModelTests`: name or summary.
- `ItemListTests`: a `ListSearch` narrows the rows with the dropdown filters.
- The box itself: a UiShots render in its states (empty, with text, focused), not a test. Gallery pages for the box and the lists with a search active.

## Steps

One agent (App only, `medium`), in a worktree from main, in this order so the shared pieces exist before anything uses them: SearchBox and SearchQuery with their tests and gallery page; ListSearch in ItemList (Log, Recognition); the Commands tabs; the Gestures tab and the picker; Exclusions; the clear on hide; the guard, the READMEs (each folder touched, the App README's "How to add" if a declaration is added, this plan's status) and the handoff's acceptance list. Then the lead's review of its Reuses list against the types it added, merge, push, CI, a new version.

## Joel's acceptance check (PC)

Type "down" on the Gestures tab (tiles narrow as you type, Escape clears); on Commands › Apps type a group's name (the group with everything under it, expanded) then a command's name (the command under its group, the rest hidden); select the group, clear the box, it stays selected and expanded; Ctrl+F lands in the box from a selected row; Exclusions narrows on a name and on a process name; the gesture picker narrows with the gestures starting with the text first; close and reopen the window, every box is empty.
