# Hold-remap spike (macOS)

Throwaway console app for [plan 0002](../../docs/plans/0002-hold-remaps.md) step 0. It answers what the plan cannot know without trying, before the real engine code is written: on macOS, with the physical button swallowed and a Middle press posted instead, does Blender navigate? The answer goes to `docs/learnings/0005-hold-remaps-mac.md`; then this folder is deleted. Not part of `Augram.slnx`, so CI never builds it.

It installs a global hook that swallows input. **Only Joel runs it**, never an agent (CLAUDE.md).

## What it does

Only while Blender is the frontmost app (`org.blenderfoundation.blender`; `--anywhere` drops that check), Space becomes a hold key:

| Held | Blender gets | Blender does |
|---|---|---|
| Space + Left | Middle | orbit |
| Space + Right | Shift + Middle | pan |
| Space + Middle | Ctrl + Middle | zoom |
| Space + Left + Right | Ctrl + Middle | zoom |

- The physical button is swallowed, and so is its release, even if Space is let go first. The remap follows the buttons held *now*: press Right while orbiting with Left and it switches to zoom; release Left and it pans; and so on, without letting go. Every switch releases Middle, then presses the new one (Shift or Ctrl only around the press).
- Space itself is swallowed. A **tap** (released within 180 ms, no mouse button or wheel used meanwhile) types a space; a longer Space sends nothing, so it never starts playback.
- What it tests: whether Blender takes the hardware's left-drag (or right-drag) events that macOS keeps sending as moves of the posted Middle drag (**variant A**). If not, **variant B** swallows those drags too and re-posts each one as a Middle drag at the same spot, and logs how late the re-posts are. It also tests whether posted Shift and Ctrl reach Blender with the posted Middle, posted two ways: through SharpHook as Augram does today (`--post sharphook`) or through CoreGraphics directly (`--post native`). Variant B always re-posts the drags through CoreGraphics; `--post` picks how buttons, modifiers and the Space tap are posted.
- Not in the spike: typing rollover ("a b" typed fast with Space still down comes out "ab "), the other keys (W, E, R, F), other apps.

It quits by itself after 3 minutes (`--minutes 1` to `30`), and on Ctrl+C in the terminal; either way it releases whatever it holds first. It prints the time left every 30 s.

## Before a run

1. **Quit Augram** (menu-bar icon › Quit, or `pkill -f Augram` in a terminal). It hooks the same buttons (Middle is your stroke button) and two hooks would fight.
2. Open Blender with a scene (the default cube is fine), and know where you will test typing: a rename field (F2 over an object) or a Text Editor area.
3. Open VS Code on the repo, and its integrated terminal (Terminal › New Terminal). VS Code already has the Accessibility permission Augram uses, and the spike gets it through VS Code. Terminal.app works too once it is switched on in System Settings › Privacy & Security › Accessibility (quit and reopen Terminal after).

## Build once (from the repo root, where VS Code's terminal opens)

```
~/.dotnet/dotnet build spikes/hold-remap-mac/HoldRemapSpike.csproj
```

## The four runs, in this order

Start it through `~/.dotnet/dotnet`: the SDK lives in the home folder, so the built launcher (`HoldRemapSpike` without `.dll`) cannot find .NET ("You must install .NET to run this application"). Ctrl+C still reaches the spike.

Paste one line, switch to Blender, go through the checklist, come back and press Ctrl+C (or let it time out). Then the next line.

```
~/.dotnet/dotnet spikes/hold-remap-mac/bin/Debug/net10.0/HoldRemapSpike.dll --variant a --post sharphook
```
```
~/.dotnet/dotnet spikes/hold-remap-mac/bin/Debug/net10.0/HoldRemapSpike.dll --variant a --post native
```
```
~/.dotnet/dotnet spikes/hold-remap-mac/bin/Debug/net10.0/HoldRemapSpike.dll --variant b --post sharphook
```
```
~/.dotnet/dotnet spikes/hold-remap-mac/bin/Debug/net10.0/HoldRemapSpike.dll --variant b --post native
```

Other options: `--tap-ms 250` (tap time), `--anywhere` (Space is claimed in every app: only if the `front:` lines never say Blender while it is in front), `--minutes 5`.

## Checklist (copy once per run)

```
Run: A + sharphook
1. Space + Left orbits:                                   yes / no / jumps / other
2. Space + Right pans:
3. Space + Middle zooms:
4. Space + Left + Right zooms:
5. Rolling: Left (orbit), add Right (zoom), let go of Left (pan), add Left (zoom)… switches without letting go:
6. Space tap types a space in a Blender text field:
7. Long Space (about 1 s, no mouse) never starts playback:
8. Anything stuck afterwards (a button or Shift/Ctrl acting held; click and type in Blender and another app):
Notes:
```

Then paste the console output too: it has one line per decision with its time.

## Reading the log

```
00:41:05.120 front: org.blenderfoundation.blender (Blender: Space is claimed)
00:41:06.002 hold Space
00:41:06.180 set {Left} → Middle (switch 0.4 ms)
00:41:07.250 set {Left,Right} → Ctrl+Middle (switch 0.6 ms)
00:41:08.010 set {Right} → Shift+Middle (switch 0.5 ms)
00:41:08.700 hold up 2698 ms → no tap: used
00:41:09.020 set {} → none (switch 0.2 ms)
00:41:10.400 hold up 95 ms → tap sent
00:41:12.100 drags re-posted: 61 in 1.0 s, hook→posted avg 0.120 ms, worst 0.800 ms     (variant B only)
```

`switch` is the time from the button event in the hook to the last event posted for it. `front:` lines show which app macOS reports in front; if Blender is in front and they never say so, rerun with `--anywhere`.

## If something sticks

The spike releases what it holds on every exit path. If a button or modifier still acts held: press and release Middle once, then Shift, then Ctrl. If the hook ever fails to start, it says which app needs the Accessibility permission.
