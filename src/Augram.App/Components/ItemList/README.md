# ItemList

The lookless list a `ListScreen` renders (ADR-0002 §5c): title, filters and toolbar actions above, a header row, then one row per visible item.

## Which type for which job

| Job | Use | Notes |
|---|---|---|
| Show a `ListSpec` | `ItemList` | Filters from the spec's `ListFilter`s are applied here. When the source changes the rows refresh: at once on the UI thread, else one coalesced post (`Hosting/UiThread`). |
| The pieces (toolbar, header, rows, context menu, keys) | `ListRowBuilder` | Visual choices come from the theme through the classes it sets; item-list cells use the `Row.*` tokens (Row rule). |
