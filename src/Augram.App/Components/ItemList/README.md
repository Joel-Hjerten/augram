# ItemList

The lookless list a `ListScreen` renders (ADR-0002 §5c): title, filters and toolbar actions above, a header row, then one row per visible item.

## Which type for which job

| Job | Use | Notes |
|---|---|---|
| Show a `ListSpec` | `ItemList` | Filters from the spec's `ListFilter`s and its `ListSearch` (plan 0009: `SearchBox/SearchQuery` over the row's text, after the dropdown filters) are applied here; `EmptyText` says "No lines match 'xyz'" while the search leaves nothing. When the source changes the rows refresh: at once on the UI thread, else one coalesced post (`Hosting/UiThread`). |
| The pieces (toolbar, header, rows, context menu, keys) | `ListRowBuilder` | Visual choices come from the theme through the classes it sets; item-list cells use the `Row.*` tokens (Row rule). The toolbar is the filters, then the search box (`SearchBox`, both ways in step with the spec's binding), then the actions. |
| The find key | `ItemListAction.Find` through `ListEditing/ActionMenu` on `EditKeymap.Find` | Bound on the list, so it works from a row or a toolbar button; it focuses the search box. |
