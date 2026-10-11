# Core/Text

The words Augram's lines are made of, each with one home (plan 0007 S3): screens, dialogs, rule messages and the log call these instead of writing their own. Pure; no culture-dependent formatting (digits are invariant).

| Type | Role |
|---|---|
| `Sentence` | `Count(n, noun, plural?, zeroAsNo?)`: "1 command", "3 commands", "2 categories", and "no commands" for 0 where a caller asks for it (the command tree's group detail); `List(parts)`: "a", "a and b", "a, b and c"; `Capitalised(text)`: "scroll up" → "Scroll up" |

Nouns that belong to a subsystem's own kinds live with those kinds and count through `Sentence`: the sync item kinds' words are `Sync/SyncItemNouns` (also the import review's and the export dialog's), and a fresh item's name ("New command 2") is `Mapping/FreeNames`.

**May reference:** the BCL only. **Referenced by:** any Core folder and the App.
