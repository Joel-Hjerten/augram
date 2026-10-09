# Joel's AutoHotkey script: Space navigation in Blender

**Source:** `Blender_Spacebar_Navigation (AHK 2.0).ahk` in this folder, Joel's own (Windows, AutoHotkey v2). The origin of requirements F9 "Hold remaps": Augram is to do what this does, on both platforms, without AutoHotkey.

## Why it exists

Blender's Industry Compatible (Maya-style) keymap navigates with Alt + mouse buttons, but the tutorials use the default keymap, which also has bindings the Maya one lacks. Joel could not get a held key to drive navigation inside the default keymap, so the script turns a held Space into the navigation key and sends the default keymap's own inputs.

## What it does

Only while `blender.exe` is the foreground window (`#HotIf WinActive`):

| Input | Space held | Otherwise |
|---|---|---|
| Space | swallowed; on release, sent if it was held under 180 ms | |
| Left (held) | Middle held: orbit | Left |
| Right (held) | Shift + Middle held: pan | Right |
| Middle (held) | Ctrl + Middle held: zoom | Middle |
| W | G (grab) | W |
| E | S (scale) | E |
| R | Ctrl + Shift + Alt + R | R |
| F | Numpad . (frame selected) | F |

`+{MButton Down}` presses Shift, Middle down, releases Shift: the modifier is only held around the press, which is all Blender reads. Mouse remaps wait for the real button's release (`KeyWait`) and then release the output, so a drag stays a drag.

## Quirks Augram fixes (F9)

- **Typing rollover:** Space is only sent at its release, after any key typed while it was still down, so "a b" typed fast in a Blender text field becomes "ab ". Joel hits this often. Augram sends the pending Space before the first key that is not one of the rows.
- **Tap is only "under 180 ms":** a short Space with a click in it still sends a Space, which starts playback; Joel sees occasional accidental playback and this is the likely cause. Augram keeps the timer (a long hold sends nothing, so a held Space never starts Blender's playback) and also requires that no row was used while Space was down (Joel, 2026-10-10).
- **No rolling between buttons:** with Space + Left orbiting, pressing Right sends a second Middle down (with Shift) that Blender's orbit ignores, and releasing either button sends the Middle up that ends the drag. Left + Right never zooms. Joel wants Softimage's rolling (F9 "Rolling between buttons").
- Runs elevated (`*RunAs`) for no reason Blender needs; Augram does not.
- Windows only, and a separate program to keep running.
