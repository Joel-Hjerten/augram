# UiShots: see the UI without running Augram

Renders the main window headless to PNG files: Avalonia's headless platform with Skia drawing. No desktop window opens, and no hook, overlay or engine is started (the app's desktop start never runs), so it is safe to use from an agent on Joel's machine. Open the PNGs with an image viewer, or have an agent read them.

```
dotnet run --project tools/UiShots -- [options] [tab keys...]
```

| Option | Meaning |
|---|---|
| `--out <dir>` | Where the PNGs go (default `%TEMP%/augram-shots/out`). One file per theme and key: `dark-options.general.png`. |
| `--theme dark\|light` | One theme only (default both). |
| `--size 1000x680` | Window size in DIPs (default 1000×680). |
| `--config <folder>` | Copy the `*.json` files of a config folder (e.g. `%APPDATA%\Augram`) into a temporary one and render with them, for realistic content. The real folder is only read. Without it the window shows a fresh install. |
| `--gallery` | With no keys given, also render every dev gallery page (Debug builds have the Gallery tab). |
| keys | Tab or sub-tab keys (`commands.global`, `options`, `diagnostics.log`, a gallery page's key). Default: every tab's leaf, gallery left out. |

What it cannot show: the system's glass (Acrylic, Mica, the macOS blur) is drawn by the OS behind a real window, so the shots show the solid fallback; and the system's caption buttons or traffic lights, whose room in the title bar stays empty.
