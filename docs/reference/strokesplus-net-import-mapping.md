# StrokesPlus.net → Augram import mapping

What each part of a StrokesPlus.net config becomes when `Augram.Import.StrokesPlus` reads it: the importer's spec, kept current with its readers. The types that do it, the merge policy and the report lines are in [the importer's README](../../src/Augram.Import.StrokesPlus/README.md); the SP.net schema and Joel's real usage are in [strokesplus-net-config.md](strokesplus-net-config.md). Read both before changing a reader or a mapping.

## Step mapping (plan 0001 §C1, M2 slice)

| SP.net `Steps[].Method` | Augram step |
|---|---|
| `CloseWindow`, `MinimizeWindow`, `MaximizeOrRestoreWindow`, `ToggleWindowAlwaysOnTop` | `WindowOpStep` (Close, Minimize, MaximizeOrRestore, ToggleAlwaysOnTop) |
| `SetWindowSize(width, height)` | `WindowOpStep(SetSize, WindowSize)`; unparsable or non-positive size → placeholder + warning |
| `InvokeObjectMethodByName(methodName = Center)` | `WindowOpStep(Center)`; any other method → placeholder |
| `Delay(milliseconds)` | `DelayStep`, clamped to 0..60000 with a warning when clamped; unparsable → placeholder + warning |
| `SendVKey(virtualKey)` 173..179 | `MediaKeyStep` (173 VolumeMute, 174 VolumeDown, 175 VolumeUp, 176 NextTrack, 177 PreviousTrack, 178 Stop, 179 PlayPause), through `HotkeyMapping.MediaKeyOf`: the VK table, then Core's `MediaKeyKindExtensions.FromKeyCode`, the lookup SendKeys names use too; any other key → `HotkeyStep` with no modifiers through `HotkeyMapping` (VK → `KeyCode` table), placeholder when the key has no `KeyCode` |
| `SendHotKey(hotkey{LControl, RControl, LAlt, RAlt, LShift, RShift, LWin, RWin, Key})` | `HotkeyStep` through `HotkeyMapping.FromSendHotKey` (right-side modifiers are kept: `RAlt` alone becomes Alt with `RightHand` Alt, "RAlt+F9"; a modifier set on both sides is the plain one); placeholder when the key has no `KeyCode`. `HotkeyMapping.TryUpgrade(ImportedStep)` converts a placeholder saved before hotkeys existed |
| `SendKeys(sendKeysString)` | the steps the key string makes through `TextMapping.FromSendKeys` (text runs → `TypeTextStep`, keys → `HotkeyStep` or `MediaKeyStep`, `{DELAY n}` → `DelayStep`), spliced in place, each with the step's active flag; a string with a part that does not map → placeholder plus a warning per part |
| `SendString(characters)` | one `TypeTextStep` through `TextMapping.FromSendString`, the text as written; no text → placeholder |
| `Run(command)` | `RunStep` through `RunMapping.FromRun` (the command line cut into file and arguments); no command → placeholder |
| `MouseClick`, `SendAltDown/Up`, `SendWinDown/Up`, `ConsumePhysicalInput`, unknown, null | `ImportedStep(method, step description, parameters as name → value text)`: shows as "not supported yet", runs (skips) now, and gets a real type later |
| a step with `Active == false` | `CommandStep.IsActive = false` |

One **Info** line per distinct placeholder method per file ("3 MouseClick step(s) imported as placeholders; mouse clicks arrive in a later version."), never one per step.

## SendKeys syntax (`SendKeysSyntax.Parse`)

`SendKeysSyntax.Parse(keys, textMethod)` (the state machine is the internal `SendKeysParser`, the token names `SendKeysNames`) reads .NET `SendKeys` syntax plus the classic StrokesPlus extras; it never throws, it reports a part it cannot map in `Warnings` (one sentence each, naming the part; the caller adds the action's name) and goes on:

| Syntax | Steps |
|---|---|
| plain characters | one `TypeTextStep` per run, consecutive characters merged (also across a group without modifiers and around escaped literals), by the method asked for (default Unicode) |
| `^` Ctrl, `+` Shift, `%` Alt, `@` Win (classic) before a key, or before `( … )` for every key inside (nested groups add up) | the key as a `HotkeyStep` with those modifiers; a character under a modifier is its US-layout key (`AsciiKeyLayout`) with Shift added for a shifted character, as .NET does (`^A` is Ctrl+Shift+A, `^{+}` Ctrl+Shift+=) |
| `~`, `{ENTER}`, `{TAB}`, `{ESC}`/`{ESCAPE}`, `{BS}`/`{BKSP}`/`{BACKSPACE}`, `{DEL}`/`{DELETE}`, `{INS}`/`{INSERT}`, `{HOME}`, `{END}`, `{PGUP}`, `{PGDN}`, arrows, `{F1}`–`{F24}`, `{ADD}`/`{SUBTRACT}`/`{MULTIPLY}`/`{DIVIDE}` (keypad), `{CAPSLOCK}`, `{NUMLOCK}`, `{SCROLLLOCK}`, `{PRTSC}`; classic `{WIN}`, `{LWIN}`, `{RWIN}`, `{APPS}`, `{F_1}`…, `{NUMPAD0}`…, `{DECIMAL}`, browser and media names; `{SPACE}`; names ignore case (`SendKeysNames`) | a `HotkeyStep`, or a `MediaKeyStep` for a media key with no modifier (`{VOLUP}`) |
| `{KEY n}`, `{x n}` | the key n times (at most `SendKeysSyntax.MaxRepeat`, 100, with a warning above it); a character n times into the text |
| `{+}`, `{^}`, `{%}`, `{~}`, `{(}`, `{)}`, `{{}`, `{}}`, `{@}`, any `{x}`; classic `{PLUS}`, `{CARET}`, `{PERCENT}`, `{TILDE}`, `{AT}`, `{LPAREN}`, `{RPAREN}`, `{LBRACE}`, `{RBRACE}` | that character, as text |
| `{DELAY n}` (classic) | `DelayStep(n)`, clamped to 60000 ms with a warning |
| `{VKEY n}` (classic; decimal Windows virtual-key code, space optional) | that key through `HotkeyMapping.FromVirtualKey`, with any modifiers |
| an unknown name (`{BREAK}`, `{CLEAR}`, `{HELP}`, `{BEEP …}`), classic `{DELAY=n}`, a modifier that reaches no key, a character a US keyboard lacks under a modifier, an unmatched `(`, `)` or `{` | a warning; unmatched brackets are typed as text, everything else is skipped |

Every `SendKeys` and `SendString` value in the reference config (Joel's, 2026-10-08: game console text such as a backtick or `fov 70`, punctuation-heavy text, and in scripts Ctrl/Shift hotkeys with `{TAB}`, `{PGUP}`, `{ADD}`…) parses with no warning; the tests reproduce those shapes synthetically.

## Script actions (`ScriptMapping`)

A script-only action becomes steps when its script is one of the shapes below; anything else stays a `Script` placeholder with the "script-only action" note.

| Script | Steps |
|---|---|
| one plain `sp.RunProgram(…)` call (`RunProgramScript`) | the step `ProgramCallMapping.ToStep` makes: Display mode for a Display Changer call, else Run |
| nothing but comments and blanks (SP.net's "Ignore Close" and friends: "Do nothing here, the purpose of this is to ignore the default … action on the desktop") | **no steps**: the command does nothing here, an override to nothing that silences the Global gesture in that group |
| one `sp.SendKeys("…")` call with a plain string literal, nothing else | the steps `SendKeysSyntax.Parse` makes, when every part maps (`^{ADD}` is Ctrl + Num +, `^{SUBTRACT}` Ctrl + Num −); a part that does not map keeps the placeholder |
| `clip.Clear()` alone | `ClearClipboardStep` |
| SP.net's "Window Snap to Left / Right (half screen size)" sample, margin 0 (`SampleScripts`) | `WindowOpStep` `SnapLeftHalf` / `SnapRightHalf` |
| the forum's Ctrl + wheel zoom: `WM_MOUSEWHEEL` with `MK_CONTROL` and `WHEEL_POS` / `WHEEL_NEG` (±120) posted to `sp.WindowFromPoint(action.Start, false)` (`SampleScripts`) | `ScrollStep` up / down, one notch, Ctrl held, at the gesture start |

`SampleScripts` keeps the samples' code (comments dropped) and compares token lists: a sample someone edited (another margin, two notches, a statement added) is not guessed at and stays a placeholder. Against the reference config (read 2026-10-09 through the saved Augram config, never copied): all 12 script placeholders map: Clear Clipboard, both snaps, the Global Zoom In / Zoom Out (`sp.SendKeys("^{ADD}")` / `"^{SUBTRACT}"`, the wheel post commented out beside them), the five Windows Desktop "Ignore …" overrides (comments only) and the Explorer Zoom In / Zoom Out wheel posts.

## Action mapping

| SP.net | Augram |
|---|---|
| `GestureName` resolves (case-insensitive) to a gesture imported from the same file | `Trigger.ForGesture(id)`; SP.net binds by name, so a duplicated source name binds to the first |
| `GestureName` names nothing | `Trigger.None` + warning "gesture 'X' not found; command imported without a gesture" |
| `GestureName` empty and `WheelUp` / `WheelDown` | `Trigger.ForWheel`; both set → wheel up + warning |
| neither, but keys or buttons held | a **click trigger** (`Trigger.ForClick`: the stroke button clicked while holding them; SP.net's no-gesture action) |
| none of these | `Trigger.None` + warning |
| `Control`, `Alt`, `Shift`; `Left`, `Middle`, `Right`, `X1`, `X2`; `Capture` 0 / 1 / 2 | the trigger's "while holding" set (`TriggerHold`, `TriggerReader`; learnings 0003 §3.8): the keys, the buttons with the stroke button, capture Before / After / Either (SP.net's order; missing is Either). The command keeps its `Active` flag (until 2026-10-09 these imported inactive with a "needs modifier/rocker support" note) |
| `UseSecondaryStrokeButton`, a wheel action, `SecondaryStrokeButton` set in the file (WinForms `MouseButtons`) | the wheel trigger holds that button instead of the stroke button ("X1 + wheel up"), active as in the source |
| `UseSecondaryStrokeButton`, a wheel action, no secondary button set (Joel's file) | "Right + wheel" (Joel's own use, requirements F1), **inactive**, note "a secondary-stroke-button command … check the button to hold, then switch it on" |
| `UseSecondaryStrokeButton`, a gesture or click action | the stroke-button trigger, **inactive**, note "drawn or clicked with the secondary stroke button, and Augram draws and clicks with the stroke button only" |
| a click trigger whose steps re-send a mouse click (`MouseClick`; SP.net's "Shift+Right Click" workaround) | **inactive**, note "Augram passes a click with keys held through to the app itself when nothing is bound to it" (Joel, 2026-10-09) |
| a trigger already bound in the group (A7: overlapping, combinations included) | the active command keeps it; the other is imported with `Trigger.None` + warning naming the combination ("Shift + gesture 'Up'") |
| `Steps` non-empty | one `CommandStep` per entry (a script beside steps is ignored, as SP.net ignores it) |
| `Steps` empty, `Script` of a shape `ScriptMapping` recognises (above: `sp.RunProgram`, comments only, `sp.SendKeys`, `clip.Clear()`, the snap and Ctrl + wheel samples) | the steps it makes (none for comments only), no note |
| `Steps` empty, any other `Script` | one `ImportedStep("Script", name, { script })` so the script is visible on the step list, plus `Note` "Imported from StrokesPlus.net: script-only action"; `Active` carried over |
| both empty | a command with no steps: the override to nothing, no warning |

## Category mapping (Joel, 2026-10-07)

| SP.net | Augram |
|---|---|
| `GlobalApplication.Categories[]`, `Applications[].Categories[]` (strings) | the group's `Categories`, fresh `CategoryId`s, the list's spelling (trimmed; empty and repeated names ignored, the first spelling wins) |
| `Action.Category` naming a listed category (case-insensitive) | the command's `CategoryId` |
| `Action.Category` empty or missing | no category (`CategoryId` null); in Global the rules then put the command in the Uncategorized category, made with its fixed id (Joel, 2026-10-11) |
| every action of the group in one category named `General` (or the group has no actions) | **no categories at all**, no command in one (in Global: all in Uncategorized): SP.net gives every app "General", so this is noise (18 of the reference config's 19 apps) |
| a listed category no imported action uses | left out, no warning |
| `Action.Category` naming a category the list lacks | the category is created with the action's spelling; **Info** "Category 'X' is not in the application's category list; created for its commands.", once per category per group |

"General" beside other used categories is kept like any other (the reference Photoshop group: General plus four "Blend Mode …" categories).

## Matcher mapping (F5)

| SP.net | `AppMatcher` |
|---|---|
| `FileName` non-regex | `ProcessNames = [value]` |
| `FileName` regex that is a plain alternation of literals (`a\.exe\|b\.exe`, optionally in one group or anchored) | `ProcessNames` split into the names |
| `FileName` any other regex (`PotPlayerMini.*\.exe`) | warning; `ProcessNames` left empty (fill in by hand) |
| `FilePath` | `ProcessPath` + `ProcessPathIsRegex` |
| `RootWindowText`, else `OwnerWindowText`, `ParentWindowText`, `ControlWindowText` | `Title` + `TitleIsRegex`; a warning names the fields that differ from the one used |
| `OwnerClassName`, `RootClassName`, `ParentClassName`, `ControlClassName` non-regex | one `ClassChain` entry each; a plain-alternation regex becomes one `a\|b` entry; any other regex is skipped with a warning |
| `ControlID` set | warning, skipped |
| `IgnoreFullScreen` | `IgnoreWhenFullScreen` (A21) |
| a regex the engine refuses | dropped (path first, then title) with a warning, so the group still imports |
