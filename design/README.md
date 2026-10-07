# design/ — source artwork

Human-authored design source: editable working files and clean master exports. **Not shipped**: the app embeds only what
`tools/IconTool` generates from here. Same layout as Joel's Eyeris repo.

```
app-icon/            THE app mark: app-icon.psd (Photoshop working file) and exports/app-icon.png (1024 px master)
```

## Source vs. generated

```
design/app-icon/exports/app-icon.png       source of truth (edit the .psd, export here)        committed
src/Augram.App/Icons/augram.{ico,icns}     the .exe icon and the future macOS bundle icon       generated, committed
src/Augram.App/Assets/Icons/augram-256.png every window's icon                                  generated, committed
src/Augram.App/Assets/tray-*.png           tray icons (Windows colour pair, macOS template pair) generated, committed
```

Never edit the generated files by hand: change the art, export the master, run `dotnet run --project tools/IconTool`,
commit the art and the outputs together. `tests/Augram.App.Tests/Tray/IconAssetsTests` pins every generated size.

## Design guidelines (from Eyeris)

- Square, transparent, centred, with a small safe margin; full-bleed art at 1024 px, downscaling handles the rest.
- Test at 16, 32 and 256 px: fine detail disappears at 16, so keep the silhouette bold.
- The macOS menu bar uses a template image (shape and alpha only; the system tints it). The tool derives it from the master,
  with the white strokes cut out as holes. A hand-drawn `exports/app-icon-mac-tray.png` (one dark fill, highlights as real
  holes, square) wins when it exists.
