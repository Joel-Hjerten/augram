# Plan 0003: Export and import of Augram JSON

**Status: PROPOSED (2026-10-10, night).** Written by an agent while Joel slept; the decisions marked "Decided for Joel" below were taken on his behalf and wait for him. **Progress:** steps 1–2 (Core) **built, reviewed and merged** (`5de531b` groundwork, `6a0b1fd` `Core/Transfer` with 47 tests; merge `0734bd8`); steps 3–4 (App) **built** to the sketch below (`cc86f06` export, `9fede87` import; 2026-10-10 night), **reviewed by the lead and merged** (`3fed683`); since then plans 0004 and 0005 carried their new data through it (schemas 5–7). Joel's check (step 5) not done yet; follow-ups from his questions of 2026-10-11 below. What the App build decided on Joel's behalf is decisions 19–28 below. This is plan 0001 M2 step 9. The *what* is [requirements F8](../requirements.md) ("Export is the same format as the on-disk file…", read the sync section too) and F5a's Copy leaning; this plan says how, where the code goes and in what order. Structure rules: [ADR-0002](../adr/0002-code-and-repo-structure.md).

## Done when

On one machine Joel exports his Blender app group to a file; on a config that lacks it (a fresh profile, a friend's machine) he imports the file and the group arrives whole: its matcher, the Space hold remap, its eight commands, the gestures they use. Concretely:

- **Export…** writes one of three scopes to a `.augram.json` file: everything (the options without the sync section, every gesture, the whole mapping), gestures only, or selected app groups (Global selectable) and ignored apps with the gestures their commands use. The dialog says how many steps type text or run command lines, since those travel as written.
- **Import…** reads any Augram JSON (an export, `augram.json` itself, a file from `backup/`, a sync machine file), shows what is new, what is the same and what differs, offers Keep mine / Take theirs / Keep both per differing item, applies as one undo step per store and reports what it had to repair.
- Importing a file just exported from the same config changes nothing. Importing it twice changes nothing the second time.
- A file written by a newer Augram is refused with a message that says so; an older one is migrated as the config file is.

## Decisions (Joel's, from the requirements)

1. **Same format as the on-disk file**, never a second format (F8). An export is the config file with some members left out; any Augram JSON imports.
2. **Scopes:** everything · gestures only · selected app groups with the gestures they reference (F8).
3. **Import merges rather than replaces**, with a per-item conflict choice: keep mine / take theirs / keep both renamed (F8).
4. **Settings are per machine** (F8 sync: "Settings stay per machine … the sync settings themselves"); sync settings never travel.
5. **Copy of a command or step places Augram JSON on the clipboard** (F5a, LEANING): a later step (6), built on the same writer.

## Decided for Joel (2026-10-10 night, to confirm)

Each line: the decision, then why. Overrule any of them and the lead changes that line of code.

1. **"Everything" is the options (without `sync`), every gesture and the whole mapping** (groups with categories, hold remaps, commands and own versions; ignored apps). The sync section is never written: it names this machine (its id would make two machines write one sync file) and its repository.
2. **An import changes options only when asked**: a file that carries options (an "everything" export, `augram.json`) offers "Also use its options", off by default. It takes Capture, Trail, Recognition, No match, and General's stroke button and ignore keys; never Sync, Start at login, Enabled or the menu-bar icon (state of this machine, not preferences). One undo step on the Options store. Why: settings are per machine (decision 4), but a new machine set up from a file wants them.
3. **File extension `.augram.json`**, suggested name `Augram <what> <yyyy-MM-dd>.augram.json` ("Augram everything 2026-10-10", "Augram gestures …", "Augram Blender …" for one group or ignored app, "Augram 3 groups …" for several groups, "Augram selection …" for a mix; characters a file name cannot hold become "-"). Why: `.json` opens in any editor (F8: safe to hand-edit), the inner `.augram` says whose it is. The import picker shows `*.augram.json` and `*.json`, so `augram.json`, a backup or a sync file can be picked too.
4. **A selection carries the gestures its commands use, nothing else of the library**: every gesture a selected command's trigger or own-version trigger names, whole (samples and the original kept by shape cleanup).
5. **Ids are kept on export and on import**, so re-importing a file lines up item by item (same → nothing to do; changed → a choice). **An imported item whose id is unknown here is matched by name** (a group by name; a category, a command within its parent, a hold remap by hold key then name, within the matched group; an ignored app by name; a gesture by name, then **by shape at 90 or more** like the StrokesPlus.net import, A7) and takes the local id, so a file from another machine or person lines up too. Why: two configs that grew apart (two StrokesPlus.net imports, a friend's export) share names, not ids; without matching every item would arrive as "Chrome (2)".
6. **An imported command whose gesture exists here with different samples follows the gesture's choice.** The gesture is a differing item like any other: Keep mine → the command binds to your gesture (your shape draws it); Take theirs → your gesture (same id, every command using it) gets the file's shape; **Keep both → the file's gesture is added beside yours (renamed on a clash) and the commands the import brings bind to it**, while your commands keep yours. Why: Keep both means "I want their gesture too", and their commands were made for it.
7. **Keep both gives the copy a new id** and renames it on a name clash with the sync's rule (" (2)", " (3)"…: `SyncNames`, since 2026-10-11 `Core/Mapping/NameScope`), not the StrokesPlus.net import's " (imported)": one rule and one code path for every merge. Since 2026-10-11 (Joel) it is the one rule for every clash: paste, a picked app and the StrokesPlus.net import too.
8. **Keep both applies to gestures and commands only**; for a group, category, hold remap, own steps or ignored app the nearest choice (Take theirs) is applied with a note, exactly as the sync does (`ConflictResolution.Effective`). A command's own steps follow the command's choice when the command differs (Keep mine leaves yours, Take theirs takes the file's, Keep both puts the file's on the copy); otherwise they are an item of their own.
9. **Default choice Keep mine**, with "apply to all", as in the StrokesPlus.net import and the sync dialog. Nothing is lost by accident.
10. **An import never deletes**: an item the file lacks stays. (The sync join's "use the synced settings" replace is not offered; see the questions.)
11. **Schema newer than this build: refused** ("The file was written by a newer Augram (schema version 5); this build reads up to version 4."); **older: migrated** through `ConfigMigrations`, the config file's own path. A step type this build lacks under the same schema is imported as is (kept, read-only, skipped when run; `Steps/Unknown`) with one notice per step, as on load.
12. **A file that breaks a rule is refused whole**, naming the rule (duplicate names, a gesture bound twice in a group), as the sync refuses a machine file. Only hand edits can do that.
13. **Hold remaps and categories travel with their group**: a group is exported whole. On import they are items of their own, matched within the group (decision 5).
14. **Global is exportable as a group**: listed first in the selection, exported with its categories and commands. Every exported file has a Global group (the format requires one); when Global was not selected it is an empty shell, which an import ignores entirely (not even its active flag).
15. **Ignored apps are selectable beside the groups** in the selection, and are all in "everything".
16. **Import report:** before Apply, the counts of new / same / different per kind, the items matched by name or shape ("'Chrome' in the file is your 'Chrome'"), one row per differing item with its choice, the repairs the result needs (renamed, unbound) and the file's notices; after Apply, what changed per kind, the repairs, the notes, and one Info line to the log (source `import`, counts only, never content).
17. **Undo: one step per store that changed** (gestures, mapping, options), like a sync apply; the config file's backup before every write keeps the previous file.
18. **Private text:** Type text, Run and imported StrokesPlus.net placeholder steps travel as written. The export dialog counts them ("3 steps type text or run command lines; they are in the file as written. Do not share it if they hold passwords.") and strips nothing; the type says so itself (`IStepType.MayHoldPrivateText`, N3), never a list of type keys.

Decided while building the App (steps 3–4, 2026-10-10 night):

19. **Options › Configuration is two rows and a result line:** "Augram file" with Export… and Import…, "StrokesPlus.net" with "Import from StrokesPlus.net…", then "Last result" (the last export's or import's line) once something ran. Why: one row per kind of file reads clearer than three buttons in a row, and the outcome has to show somewhere on a page with no message line.
20. **Export… sits on a section header only**: an app group, a Global category, and Uncategorized (Global for both); not on a hold remap (its group is exported whole, which the menu of a hold remap would hide) or a command (the clipboard, step 6). On the Gestures toolbar it comes after the StrokesPlus.net import.
21. **Save… is disabled while Selected has nothing ticked** (the shared `FormDialog` gained `CanConfirm` for it), rather than writing a file with nothing in it.
22. **The pickers start in the folder last picked in this run** (the OS default the first time), and a save name typed without `.json` gets `.augram.json`.
23. **Apply to all leaves a row alone when it does not offer the choice**: Keep both on all keeps an app group's or a category's row at its own choice (Keep mine by default) instead of turning it into Take theirs. Why: decision 9, nothing is lost by accident; the row still shows what it will do.
24. **Import applies the plan the user reviewed**, and plans again from the stores (with the same choices, up to three times) only when the stores refuse it because they moved meanwhile. The sketch planned again first; the result is the same when nothing moved, and this way what is applied is exactly what was shown.
25. **After Import the review closes and a message gives the summary and the repairs** ("Imported from Blender.augram.json: 1 app group, 1 hold remap and 2 commands added; 1 command changed." then one repair per line); the summary is also Options' "Last result". Undo is per tab (Gestures, Commands), as for a sync; taken options have no undo in the UI (the config backup keeps the previous file).
26. **The matched items are listed one per line, at most 12**, then "…and N more." A friend's whole configuration matched by name would otherwise push the differing items off the window.
27. **The review names the file's side "In the file" and this side "Yours"** in the conflict list's column titles (the shared list gained `MineHeader` / `TheirsHeader`); the rows say "Gesture · with Blender.augram.json".
28. **The logs carry counts only**: `export` the scope, the contents line ("1 gesture, 1 app group, …") and the private-text count; `import` the `SyncCounts`, whether options were taken, the repair and note counts and how often it planned again. Neither logs a file name or path.

## Questions for Joel

1. **Should an import be able to replace** (remove what the file lacks, like the sync join's "Use the synced settings")? *Built: no, merge only (decision 10).* A replace would be one more radio in the import dialog, for restoring an export over a config gone wrong; `backup/` covers that today.
2. **Options in an import: opt-in (built, decision 2), or never?** And is the list right (stroke button and ignore keys yes; start at login, enabled and the menu-bar icon no)?
3. **Match by name and shape (built, decision 5), or by id only?** By id only would make a friend's file arrive as "Chrome (2)" beside your Chrome; by name it shows one "Chrome: yours / theirs" row instead.
4. **Where should Export… and Import… live?** *Built as proposed (steps 3–4; decisions 19–20):* both in a new **Options › Configuration** section (before About), with the StrokesPlus.net import beside them (it also stays on the Gestures toolbar until you say otherwise), plus "Export…" on an app group's right-click menu in Commands (that group preselected) and on the Gestures toolbar (gestures only preselected). SP.net has one Import/Export button in its top bar.

## Follow-ups from Joel's questions (2026-10-11, proposed, awaiting his yes)

What the questions established (answered from the code, not yet run by Joel):
- **No pile-up from repeated imports:** ids match first, then names (a hold remap by its key, a gesture optionally by shape), so the same file twice changes nothing the second time. New items arrive only when they are new here or when Keep both makes a renamed copy. An import never removes anything, so it cannot tidy either.
- **A redrawn gesture keeps its id**, so a file from Joel's other machine matches it by id (shape matching is only for ids unknown here). Keep mine (the default) leaves the old shape without a word; Take theirs gives every command here the new shape; **Keep both adds a copy nothing uses** when the file's commands already exist here unchanged: `ImportResolution.Take` returns for an identical item before `Rebound`, so only commands new to this config bind to the copy.
- **By sync a one-sided redraw needs no choice:** the three-way merge takes it on the other machine's next sync; only a change on both sides is a conflict (Core Sync README "Conflicts show on one machine").

Proposed (Joel has not said yes yet):
1. **Replace, per group, in the import review** ("make my Blender exactly what the file holds", removing what the file lacks; one undo step; groups not in the file untouched). The narrow answer to Question 1.
2. **A "Shape differs" mark on a redrawn gesture's row**, so a quick Import does not skip a redraw unnoticed (rather than defaulting to Take theirs, which is risky for files from other people).
3. **No Keep both on a gesture nothing in the file would bind to**, or a warning that the copy would be unused.
4. **A warning when Keep both on a command gives an unbound copy** (its trigger is taken here).

## Design

### Core (`Core/Transfer/`, new, with a README)

A folder of its own because it sits on two subsystems: `Config` (the format: reader, writer, migrations) and `Sync` (the item merge and its repairs). Putting it in `Config` would make Config depend on Sync (today Sync depends on Config, not the reverse); putting it in `Sync` would mix files with machines. Everything here is pure except `ImportResult.ApplyTo`, which calls the stores.

| Type | Where | Role |
|---|---|---|
| `TransferFile` | `Transfer/` | an Augram JSON file: `Gestures`, `Mapping` (null = not in the file), `Settings` (null = not in the file; never with sync), `SchemaVersion` as written, `Notices` from reading; `Extension` = `.augram.json` |
| `TransferSerializer` | `Transfer/` | `Write(file)`: the config file's envelope with absent members left out and `settings` without `sync`, through `ConfigJsonContext` and `MappingJsonWriter` (as `SyncFileSerializer` does). `Read` / `TryRead(json, steps)`: `ConfigSerializer`'s parse, version check, migrations and deserialisation (refactored so the subject of its messages can be "The file"), then `GestureRules`, `MappingRules`, `SettingsRules`; a broken file is one error line |
| `ExportScope` | `Transfer/` | closed set: `Everything`, `GesturesOnly`, `Selection(groups, ignored apps)` |
| `Exporter` | `Transfer/` | `Export(scope, ConfigDocument current)` → `TransferFile`; `SuggestedFileName(scope, mapping, date)` |
| `TransferContents` | `Transfer/` | what a file holds, for both dialogs: gestures, app groups, Global (with content), categories, hold remaps, commands, ignored apps, options, and the steps that may hold private text |
| `ImportMatcher` (internal, + `.Mapping.cs`) | `Transfer/` | decision 5: gives an imported item with an unknown id the id of the local item it matches by name, hold key or shape, and rewrites the references (a command's gesture, category, hold remap) |
| `ImportPlan`, `ImportEntry`, `ImportStatus`, `ImportMatch`, `ImportMatchKind`, `ImportOptions`, `ImportChoices` | `Transfer/` | `ImportPlan.Create(file, ConfigDocument current, options)`: the file matched and split into sync items, one entry per item (New / Same / Different, with how it was matched), the conflicts as `SyncConflict`s (the source's name in `MachineName`, so the App's conflict list reads "with Blender.augram.json"), whether the file's options would change anything, and `Preview` (every choice Keep mine). `Resolve(choices)` → `ImportResult` |
| `ImportResolution` (internal) | `Transfer/` | the choices applied to the items (decisions 6–8), then `SyncDocumentBuilder.Build` with the file's items as incoming: the sync's repairs, validation and counts; `WithOptionsFrom(current, file)`, decision 2's one home |
| `ImportResult` | `Transfer/` | the gestures, mapping and options the import leaves, `SyncCounts`, `SyncRepair`s, notes; `ApplyTo(settings, gestures, mapping)`: one `ReplaceAll` / `Apply` per store that changed, refused (false, nothing applied) when a store moved since the plan was made (a sync applied meanwhile), so the caller plans again |
| `IStepType.MayHoldPrivateText` | `Steps/` | default false; true for Type text, Run and Imported (decision 18) |
| `Command.WithGesturesReplaced(map)`, `Command.GestureIds()` | `Mapping/Command.Trigger.cs` | the gestures a command's triggers name, and an id map applied to both triggers at once; `MappingImport.Rebind` (StrokesPlus.net) uses it too and so covers own-version triggers |

### An import is a sync merge without a base

The sync's merge table with an empty base keeps three rows: present only in the file → **New** (added), the same content on both sides → **Same**, present on both sides and different → a **conflict** that keeps mine until chosen. No row deletes, which is decision 10. So the import reuses, unchanged: the items and their keys and canonical contents (`SyncItem`, `SyncItemSet`, `SyncItemKey`), the conflict record and the choices (`SyncConflict`, `SyncChoice`, `ConflictResolution.Effective`), the rebuild with its repairs (`SyncDocumentBuilder`: name clashes " (2)", A7 unbinding, dangling references, the hold remap rules, Global restored), `SyncCounts` and `SyncRepair`. Two things are the import's own: matching by name and shape (the sync always has ids on both sides), and Keep both on a gesture rebinding the file's commands to the copy. With every choice Keep mine the result equals `ThreeWayMerge.Merge(no base, mine, file)`; a test holds that.

### Matching (decision 5)

| Kind | Matched by, in order | Within |
|---|---|---|
| gesture | id, name, shape (first sample ≥ `ConfusionCheck.DuplicateCutOff`, when `ImportOptions.MatchShapes` is set) | the library |
| app group | id; Global always to Global; name | the mapping |
| category | id, name | the matched group |
| hold remap | id, hold key, name | the matched group |
| command | id, name among its siblings (the same hold remap, or the group's ordinary commands: `CommandNames`) | the matched group |
| ignored app | id, name | the ignore list |

A local item already matched by id is never matched again by name, so two items never share a key. A file item matched by name takes the local id and its references follow, so it then compares like a re-import of the same item.

### The file

```json
{
  "schemaVersion": 4,
  "settings": { "general": { … }, "capture": { … }, "trail": { … }, "recognition": { … }, "noMatch": "DoNothing" },
  "gestures": [ … ],
  "mapping": { "groups": [ { "id": "00000000-0000-4000-8000-000000000001", "name": "Global", … , "commands": [] }, { "name": "Blender", … } ], "ignored": [] }
}
```

`settings` only in "everything" (no `sync` member), `mapping` absent for "gestures only", `gestures` always (maybe empty). Read back by `ConfigSerializer`, such a file is a valid config: a missing section takes its default.

### App (sketch; built 2026-10-10 to it, see the App README "Export and import of Augram files")

A folder `App/Transfer/` beside `Import/` (StrokesPlus.net) and `Sync/`, registered by the module that registers `Import/` today (`Hosting/GesturesModule`) or a `TransferModule` of its own. Nothing in it decides what is exported or merged; it calls `Core/Transfer` and shows the answer.

*As built:* a `Hosting/TransferModule` of its own; the presenters, the file picker and the review window in `App/Transfer/`; the view models beside the others in `ViewModels/` (`ExportViewModel`, `AugramImportViewModel`, `ConfigurationViewModel`); the review's declared top in `Screens/AugramImportScreen`; and decisions 19–28 where the sketch left a detail open or (24) said otherwise.

- **Entry points** (Question 4, proposed): Options gets a **Configuration** section before About (`OptionsScreen`, a `FormScreen` section like `OptionsSyncSection`) with **Export…** and **Import…** buttons, and the StrokesPlus.net import beside them (it stays on the Gestures toolbar too until Joel says otherwise); **Export…** on an app group's and a category's right-click menu in Commands (that group preselected; Global for a category of Global); **Export…** on the Gestures toolbar (gestures only preselected). The tray stays as it is.
- **`IExportPresenter` / `ExportPresenter` + `ExportViewModel`.** The scope dialog is a `FormDialog` (the shared dialog chrome the join question uses), declared by `ExportViewModel.Declare()`: Scope as a `ButtonRadioField<ScopeKind>` (Everything / Gestures only / Selected); under Selected a check list of Global, the app groups (by name, "Windows only" where Use on says so) and the ignored apps, which needs a check-list field kind (`Fields/CheckList/`, self-registering like every field kind; the one new component); a line from `TransferContents` ("2 app groups, 14 commands, 6 gestures"); when `PrivateTextSteps > 0`, a `NoteField`: "3 steps type text or run command lines; they are in the file as written. Do not share it if they hold passwords." Confirm is **Save…**: the storage provider's save picker with `Exporter.SuggestedFileName(scope, mapping, today)` and the filter "Augram files (*.augram.json)", then `TransferSerializer.Write` to the path (temp file + move, like `FileConfigStore`). Logs Info `export` "Exported Augram file" with the scope and the contents line, never content.
- **`IAugramImportPresenter` / `AugramImportPresenter`.** The open picker ("Augram files (*.augram.json)", "JSON (*.json)"), `TransferSerializer.TryRead` (the error line in a message box when it fails: "The file was written by a newer Augram …"), then **`AugramImportWindow`** over **`AugramImportViewModel`**, modal to the main window like `SyncConflictWindow`.
- **`AugramImportViewModel`** (built from the `ConfigSession`, the file and its name): `ImportPlan.Create(file, session.Document, new ImportOptions { SourceName = fileName, MatchShapes = settings.Recognition })`; the header ("Blender.augram.json: 1 app group, 10 commands, 2 gestures"); the counts ("New: 1 app group, 10 commands · Same as yours: 2 gestures · Different: 1"); one line per `ImportEntry.Match` ("'North' in the file has the shape of your 'Up' (96)", "'Chrome' in the file is your 'Chrome'"); the differing items through **`SyncConflictsViewModel`** and the **`SyncConflictList`** component, both reused as they are (Keep mine default, Keep both offered for gestures and commands by `Allowed`, the side summaries and glyphs), with "Apply to all" (Keep mine / Take theirs / Keep both, as the StrokesPlus.net import has) and an import help line in place of the sync's (`SyncConflictsViewModel.Help` becomes a parameter); **Also use its options** (a toggle shown only when `ImportPlan.HasSettings`, with "stroke button, trail, capture and recognition; not start at login" as its help); the preview's repairs and notes, recomputed with `plan.Resolve(choices)` on every change; the file's notices; **Import** / Cancel, Import disabled while `ImportPlan.IsEmpty` ("Everything in this file is already here."). Import plans again from `session.Document`, resolves with the same choices and calls `ApplyTo` on the UI thread (a refusal there can only mean the stores moved between those two lines, so plan once more), shows "Imported: 1 app group, 10 commands added; 1 command changed" with the repairs, and logs Info `import` "Imported Augram file" with `SyncCounts` and the repair and note counts.
- **Dev gallery:** a Transfer tab with the export dialog in each scope (with and without the private-text note) over `CommandGalleryFakes`, and the import review over a plan made from the same fakes and a changed copy of them (new, same, different, matched by name and shape, a repair).
- **Tests (headless, `Augram.App.Tests`):** the export view model's scopes and counts; the import view model against an in-memory session (choices, apply to all, options toggle, the apply and its undo steps); no real file pickers (the presenters are behind their interfaces, as `IImportPresenter` is).

## Steps

1. **Core: the file and the export (built 2026-10-10, `5de531b`, `6a0b1fd`).** `Transfer/` with `TransferFile`, `TransferSerializer`, `ExportScope`, `Exporter`, `TransferContents`; `ConfigSerializer.Read` split so a parsed tree and a message subject can be passed; `IStepType.MayHoldPrivateText`; `Command.GestureIds` / `WithGesturesReplaced`; READMEs. Tests: each scope round-trips through the writer and the reader (and through `ConfigSerializer.Read`, as a config); no `sync` member ever; the Global shell; the referenced gestures (own-version triggers included); suggested names; the private-text count; a newer schema refused, an older migrated; a file breaking a rule refused; an unknown step type kept with a notice.
2. **Core: the import (built 2026-10-10, `6a0b1fd`).** `ImportMatcher`, `ImportPlan` and its records, `ImportResolution`, `ImportResult`. Tests: into an empty config; into an identical one (nothing to do); changed items with each choice; Keep both on a gesture rebinding the imported commands; name clashes renamed; matching by name (group, category, hold remap by key, command, ignored app, gesture) and by shape; a command's own steps following its command; hold remaps and categories carried with their group; options taken or not; `ApplyTo` as one undo step per store and refused when a store moved; Keep mine everywhere equal to the sync's two-way merge.
3. **App: export (built 2026-10-10, `cc86f06`).** The dialog, the entry points from Question 4, the gallery entry; the check-list field kind. Tests: the scopes each entry point preselects, the list, the counts and the typed-text note, an empty selection, the suggested name, the form; the presenter with a faked dialog and picker (the file reads back, counts-only log, Cancel at either step, a failed write); the entry points; the field kind; the gallery page.
4. **App: import (built 2026-10-10, `9fede87`).** The review window, apply, the report, the gallery entry. Tests: over an in-memory session, the Blender group arriving whole in a configuration that lacks it, a file already here, choices and one undo step per store, apply to all, the options toggle, matching lines, a repair and the choice that frees it, the stores moving while the review is open, Cancel; the presenter with a faked picker and windows (summary, Cancel, a newer schema, an unreadable file); the rendered review; the gallery page.
5. **Joel's check:** the "Done when" list on one machine (a second config folder through the existing folder option stands in for "a machine that lacks it"). Never touches the real config:
   1. Pull main. Quit the installed Augram from the tray (or answer "Quit it and start this one" when the dev build asks).
   2. Make two test folders with a copy of the config, sync removed from the copy (sync settings live in the folder, so the real config is untouched). In PowerShell:
      ```
      New-Item -ItemType Directory C:\Temp\augram-source, C:\Temp\augram-fresh
      Copy-Item "$env:APPDATA\Augram\augram.json" C:\Temp\augram-source\
      node -e "const f='C:/Temp/augram-source/augram.json',fs=require('fs');const j=JSON.parse(fs.readFileSync(f,'utf8'));delete j.settings.sync;fs.writeFileSync(f,JSON.stringify(j,null,2))"
      ```
   3. Start the dev build on the copy, without hooks: `dotnet run --project src\Augram.App -- --config-folder C:\Temp\augram-source --no-engine`
   4. Commands › Apps › right-click **Blender** › **Export…**: Selected with Blender ticked, "In the file" about 1 app group, 1 hold remap, 8 commands; suggested name "Augram Blender <today>.augram.json". Save it in `C:\Temp`.
   5. Also: the Gestures toolbar's **Export…** (Gestures only preselected); Options › Configuration › **Export…** (Everything; the typed-text note when steps type text or run command lines); Selected with nothing ticked greys out Save…; right-click a Global category (Global ticked).
   6. Quit from the tray. Start the dev build on the empty folder: `dotnet run --project src\Augram.App -- --config-folder C:\Temp\augram-fresh --no-engine`
   7. **Done when:** Options › Configuration › **Import…**, pick the Blender file. The review shows New: 1 app group, 1 hold remap, 8 commands (and gestures), any "Matched" lines and differing rows. **Import**: "Imported from … added"; Commands › Apps has Blender with its matcher, the Space hold remap and all eight commands, their gestures on the Gestures tab.
   8. Import the same file again: "Everything in this file is already here.", Import disabled.
   9. Ctrl+Z on the Commands tab removes Blender; on the Gestures tab, the gestures the import added.
   10. Import the "everything" file from 5: "Also use its options" is offered, unticked. Tick it, set a row to Take theirs, try Apply to all and watch "After the import" change, then Import: stroke button and trail as in the source; Sync and Start at login unchanged.
   11. Copy the Blender file, set its `"schemaVersion"` to 99 and import it: refused ("written by a newer Augram"), nothing changes.
   12. Clean up: quit the dev build, start the installed Augram, delete `C:\Temp\augram-source` and `C:\Temp\augram-fresh`. (Optional: step 6 without `--no-engine` tries the imported Space remap in Blender; say so to the lead first, it installs the hook.)
6. **Clipboard (F5a leaning):** Copy puts the copied command (or hold remap) as Augram JSON on the system clipboard (a `Selection` of the command's group holding just that command, its category or hold remap, and its gestures); Paste reads it with `TransferSerializer` and pastes like the in-app clipboard (new ids, renamed on a clash, into the selected group). A step alone is a later question (it is not an item of the file).

## Not in this plan

A passphrase or encryption (F8 sync: later, for sync and exports alike); `.augram.gz`; a replace-mode import (Question 1); exporting a single category or hold remap (the clipboard, step 6, covers single commands); a three-way import against an earlier export (an export has no base to remember); StrokesPlus.net's binary `.spexport`.
