# GestureGrid

The Gestures tab's grid (F3, F4, F5a): `Tiles` (`GestureTileItem`, sorted by the host) in, one `GestureGridActionEventArgs` out. Selecting a tile marks its duplicates (A7) with their score; selection survives a rebuild by id; right-click selects; a double click redraws.

| Type | Job |
|---|---|
| `GestureTile` | A tile on `ListEditing/ItemRow` (rename in place; no active box): glyph, name, `:duplicate`, `:partner`. Also the gesture picker's tile. |
| `GestureGridMenu` | The menu table over `ListEditing/ActionMenu`; keys from `EditKeymap` (F2 or Enter renames on Windows, Return on macOS). |
| `GestureTileItem`, `GesturePartner` | Tile data; `From(gesture, pairs)` picks the partners (it stays here: the gesture picker's presenter, in Components, builds tiles too). |
