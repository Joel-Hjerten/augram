# Steps/Imported

The **Imported (not supported yet)** placeholder (key `imported`, category Other): what the StrokesPlus.net importer stores for a step Augram has no type for yet, so Joel's commands survive an import before every type exists and show honestly as not supported. C1 routes `SendAltDown`/`SendAltUp`, `SendWinDown`/`SendWinUp`, `ConsumePhysicalInput`, `MouseClick` (until the MouseClick type lands) and anything unknown here; script-only actions keep their script as the command's note instead.

| File | Role |
|---|---|
| `ImportedStep` | record: `SourceMethod` (SP.net `Steps[].Method`), `Description`, `Parameters` (SP.net `MethodParameters` as name → value strings) · `Summary` "SendAltDown (not supported yet)" |
| `ImportedStepType` | `{ "method": "SendAltDown", "description": "…", "parameters": { "name": "value" } }`; a non-object `parameters` or a non-string value is a `StepFormatException` naming the member (`parameters.<name>`) |
| `ImportedExecutor` | always Skipped "imported step '<method>' is not supported yet", so the rest of the command still runs; one Info line |

**Never in the picker.** The type's category is `StepCategory.Other`, the one category the picker does not list; no screen needs to know this type's key. `CreateDefault()` exists only to satisfy the contract (empty method).

When a real type for a method lands, the importer maps that method to it and existing configs are migrated by a `ConfigMigrations` step; the placeholder keeps carrying the raw parameters until then.

**May reference:** `Diagnostics`, `Steps`. **Referenced by:** `StepRegistry.BuiltIn`, the importer, the App's step list (read-only row, no form).
