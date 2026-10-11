# Views

`MainWindow` only: it composes `Shell` and `InspectorOverlay` and owns no visuals; closing hides it (F7); its title is `AppInfo.Label`. `Hosting/MainWindowFactory` makes it, with the look attached, for the app and for `tools/UiShots`.

Behaviours that are not views live elsewhere: `Hosting/ClickAwayFocus` (a click outside a text box takes its focus, every window), `Hosting/MacDockPresence` (macOS: the Dock icon follows the window), the take-over's confirmation in `Dialogs/`.
