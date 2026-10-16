# Parts

Small building blocks for any component.

| Type | Job |
|---|---|
| `AskingDropdown` | A `ComboBox` that only asks: a user's pick goes to the callback, which asks the host; `Show(labels, index)` puts it on what is really stored and never echoes back as a pick; labels are replaced only when they changed. Used by the command header (trigger kind, category, drag distance, input) and `SyncConflictRow`. |
| `PlatformSideBar`, `PlatformSide` | A per-platform list's "… for: Windows · macOS" row (plans 0010, 0012): the view switch (this computer's segment outlined, `here`), Copy from, Mark as checked and a note on one line; `Label`/`Help` name it, `Side` in, `SideRequested`/`CopyRequested`/`MarkCheckedRequested` out. The Steps card's "Steps for" is `CommandHeader/StepSideBar` (a subclass mapping to the tree's actions); the While holding card's "Ignore for" is `HeldInputList`'s. |
| `TemplateParts.OnClick` | `e.OnClick("PART_X", action)` in `OnApplyTemplate`: runs the action on each click of a template's button, tolerates a template without it, returns the button. |
| `ToolbarButton.Create` | A button built in code: the `toolbar` class and its click action; the caller sets anything else (`Name`, a tooltip, `IsDefault`). |
