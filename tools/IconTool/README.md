# tools/IconTool

Regenerates every icon Augram ships from Joel's master, `design/app-icon/exports/app-icon.png` (1024 px, transparent; the
Photoshop working file `design/app-icon/app-icon.psd` sits beside it; see `design/README.md`). Not part of `Augram.slnx`
and not built by CI; run it after changing the art and commit the art and the outputs together.

```
dotnet run --project tools/IconTool [master.png] [app project folder]
```

| Output | Used for |
|---|---|
| `src/Augram.App/Icons/augram.ico` | the Windows `.exe` icon (`ApplicationIcon` in `Augram.App.csproj`, Windows builds only): 16, 20, 24, 32, 40, 48, 64, 128, 256 px as PNG entries |
| `src/Augram.App/Icons/augram.icns` | the macOS app bundle icon, for when the bundle exists: PNG entries ic07–ic14 only. Not icp4/icp5/icp6 (16/32/48 px): macOS 26 decodes PNG data there as raw pixels and shows noise (measured in Eyeris, 2026-10-07) |
| `src/Augram.App/Assets/Icons/augram-256.png` | every window's icon (title bar, taskbar, Alt+Tab), set by one style in `App.axaml` |
| `src/Augram.App/Assets/tray-enabled.png`, `tray-disabled.png` | the Windows tray: full colour while gestures are on, greyscale at 50 % while off (F7: unmistakable); 32 px |
| `src/Augram.App/Assets/tray-mac-enabled.png`, `tray-mac-disabled.png` | the macOS menu bar: a template image (black and alpha, tinted by the system; `MacOSProperties.SetIsTemplateIcon`), 36 px (18 pt @2x), the disabled one at 50 %; from `app-icon-mac-tray.png` when it exists |
| `src/Augram.App/Assets/tray-mac-colour-enabled.png`, `tray-mac-colour-disabled.png` | the macOS menu bar with Options › General › Colour menu-bar icon on (as in Eyeris): the app icon in colour, 36 px, greyscale at 50 % when disabled |

The sizes live in `IconSizes.cs`; `tests/Augram.App.Tests/Tray/IconAssetsTests` pins the same numbers. The `.ico` and
`.icns` containers are written by hand (`IconContainers.cs`), so the tool runs the same on Windows and macOS. SkiaSharp is
the version Avalonia already brings in (`Directory.Packages.props`, "Tools").
