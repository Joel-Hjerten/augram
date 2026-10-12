# Navigation

The app's tabs as data: a tree of `NavEntry`s the shell renders (F7, ADR-0002 §5c). Nothing here builds a view.

## Which type for which job

| Job | Use | Notes |
|---|---|---|
| A tab or sub-tab | `NavEntry(Title, Key, Screen, SubEntries)` | Either a screen factory (the view model is made when the shell builds) or sub-entries. |
| The app's tab list | `AppNavigation.Build(services)` | Commands, Exclusions, Gestures, Options, Diagnostics (and the Gallery in Debug), each from its module's `NavEntry` in `Hosting/Modules/`. `new NavEntry(` belongs there and in `DevGallery/`, nowhere else. |
| A tab's key | the `AppNavigation.*Key` constants | `commands.global`, `ignored.percommand`, `options.sync`… Exclusions is `ignored` in keys and code. |
| Find an entry, list the leaves | `NavigationRegistry.Find(key)`, `LeafKeys(include?)` | UiShots and the tests walk the registry through `LeafKeys`, never by hand. |
| Switch the main window to a tab | `TabNavigator.Show(key)` | Sets `MainWindowViewModel.InitialTabKey` to null and then the key, so re-selecting the shown tab still raises a change. False without a main window (gallery, tests). |
| Open one command on the Commands tab | `ICommandLocator.ShowCommand(id)` | Implemented by `ViewModels/Commands/CommandLocator`; optional, so a build without the tab shows names as plain text. |

## Adding a tab

A module under `Hosting/Modules/` with its `NavEntry`, a line in `AppNavigation.Build`, a screen under `Screens/`.
