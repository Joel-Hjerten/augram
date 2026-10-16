# CommandHeader

The selected command's header on a Commands sub-tab (F5a): name, trigger (kind, gesture glyph and picker, "While holding", a button trigger's button, drag distance), or for a command under a hold remap its Input, then category, Use on, the Not in and Also in rows, and a refused trigger's draft note with Take it and Swap. Everything only asks (`CommandTreeActionEventArgs` out, the same actions as the tree) and then shows its item again.

| Type | Job |
|---|---|
| `CommandHeader` (partials `.Hold`, `.Button`, `.DragDistance`, `.Input`, `.UseOn`, `.ExclusionRows`, `.Conflict`) | The control. `.ExclusionRows` is one parameterised row (`ExclusionRow`) for Not in and Also in. Its dropdowns are `Parts/AskingDropdown`s; its Add button… is a `Capture/ButtonDetector`; its key input a `Capture/HotkeyCaptureBox` in one-key mode. |
| `StepSideBar` | The "Steps for" row (plan 0010), a `Parts/PlatformSideBar` (the look and the switch, shared with While holding's "Ignore for", plan 0012) over the command: a Windows · macOS view switch with this computer's segment outlined (`here`), then Copy from, Mark as checked and on the other side "Edit on …", all on one line. `Item` (the `CommandHeaderItem`) in, its `StepSide` shown, `ActionRequested` (ShowStepsFor, CopyStepsFromOtherPlatform, MarkOwnVersionChecked) out. Not part of the header: the workbench puts it in `StepList.CardHeader`, the Steps card's first row (Joel, 0.14.3, as "Fields for" is of App identification). |
| `StepSide` | Its data, built by `CommandsViewModel.StepSideOf`. |
| `CommandHeaderItem` | Its item: the selected row (`CommandTree/CommandItem`, every field a row has) plus what only the header needs: `StepRefusals`, `ChangeRefusals`, `OptionsDragDistancePx`, `StrokeButton`, `ShowsDragDistance`, and the draft (`WithDraft`, `DraftNote`, `WithConflict`, `ConflictName`, `CanSwapTrigger`, `CanTakeTrigger`). Built by `CommandsViewModel.HeaderItem` from the row; a row never carries these. |

Splitting the header into `TriggerEditor`/`InputEditor` child controls is deferred (plan 0007, "Not now").
