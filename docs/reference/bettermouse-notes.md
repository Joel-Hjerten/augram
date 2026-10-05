# BetterMouse (mwenku/BetterMouse): what to learn from it

**Source:** `J:\My Drive\PROGRAMS\WINDOWS Utilities\Gesture Apps\Better mouse (gesture in c++)\BetterMouse-main`. One file, `main.cpp`, ~500 lines of Win32 C++, **MIT**. Reviewed 2026-10-05.

## What it is

Not a gesture recognizer. Hold XButton2 and the *direction* of the first few pixels of movement (2 px horizontally, 5 px vertically) fires a fixed command: virtual desktop left/right, Task View, Show Desktop. Wheel while holding steps through Alt+Tab. A tray icon and an Inno Setup installer round it out. It is a good 500-line read of a bare `WH_MOUSE_LL` hook with `keybd_event` synthesis, and nothing more.

## Worth noting

- **Hold a modifier across repeated wheel ticks, release on button-up.** The first wheel tick presses Alt and Tab; further ticks send Tab or Shift+Tab while Alt stays down; releasing XButton2 releases Alt and commits the switch. This is the right way to do "wheel-while-holding = cycle through something with a live preview" (Alt+Tab, Ctrl+Tab in browsers). Augram's wheel trigger fires a command per tick; a command that wants this behaviour needs a step type that holds a modifier until the stroke button is released. Not v1, but the pattern is recorded here for when it comes up.
- **It never suppresses the button**, only the wheel. XButton2 down/up still reach the app underneath, so the browser also navigates Back. Confirms that suppress-then-replay, which this tool lacks, is the whole difficulty.

## What not to copy

- It changes the **system-wide mouse speed** (`SPI_SETMOUSESPEED`) while the button is held and restores it on release. If the process dies mid-gesture the user keeps a slow mouse. Augram never touches global settings from the hot path.
- It warps the cursor back to the start point with `SetCursorPos` in a `Sleep` loop inside the hook callback. Sleeping in a low-level hook is exactly what gets hooks dropped by Windows (SP.net reference §9).
- **"Install as Windows Service"** in the installer: the code has no service control logic, and a service runs in session 0 where a low-level mouse hook cannot see the interactive desktop. The option is at best non-functional. Augram stays a per-user tray application started at login (checklist A10); there is no service variant to consider.
