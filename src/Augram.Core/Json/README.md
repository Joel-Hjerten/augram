# Core/Json

The one reader of a hand-editable JSON object's members (plan 0007 S4). Two fronts use it, each with its own exception and words: `../Config/JsonMembers` for the mapping subtree of the config, sync and export files (`ConfigFormatException`, "'button' of 'trigger' of command 'Magnifier' in 'Global' must be one of Left, Middle, Right, X1, X2.") and `../Steps/StepParameters` for a step's `params` (`StepFormatException`, "'key' must be one of …; got 'Thumb'."). **A new member of either goes through its front**; a new kind of file with hand-editable members gets a front of its own here.

| Type | Role |
|---|---|
| `JsonMember` (internal) | `ReadString`, `ReadBoolean`, `ReadInt32` (a JSON number; `"4"` is the wrong shape), `ReadArray`, `ReadEnum<T>`, `ReadFlags<T>`: null when the member is missing or JSON null; the front's exception for a member of the wrong shape or a name the enum lacks |

## The one rule for enums

Augram writes enums by name, so a name is all that is read: one of the enum's names, matched case-insensitively, and nothing else. A number is refused, even as a string such as `"buttons": "3"` (Joel, 2026-10-11, plan 0007 question 5: the mapping reader used to accept one through `Enum.TryParse` while step parameters refused it); so are spaces around a name and several names for an enum that is not a set of flags. A set of flags (`HeldButtons`, `KeyModifiers`) is its names separated by commas, spaces around each ignored; an empty string or an empty entry adds nothing, and "None" is a name like any other. No file Augram wrote changes meaning.

The settings sections (`../Config/AppearanceSettingsJsonConverter`, `TrailSettingsJsonConverter`) read over `Utf8JsonReader` with the source-generated context and keep their own small readers; they are not hand-editable objects in this sense.

**May reference:** the BCL only. **Referenced by:** `Config`, `Steps`.
