# Steps/Unknown

The **kept as is** placeholder (key `unknown`, category Other; Joel, 2026-10-09): a step this Augram cannot read, held so that saving does not delete it. `CommandStepJsonReader` makes one for a step whose type the registry lacks (a newer Augram's type, read by an older build) and for one whose parameters its type refuses (a value a newer Augram added), with one notice each ("Step 1 of command 'Zoom In' in 'Windows Explorer' kept as is: unknown step type 'scroll'."). The sync still skips another machine's file that has such steps (the notice is what it counts), and the format version still pauses an older build's sync.

| File | Role |
|---|---|
| `UnknownStep` | record: `StoredTypeKey` (the file's `type`), `ParametersJson` (the file's `params`, compact JSON text so equal steps compare equal), `Reason` · `StoredKey` returns the file's key, so `MappingJsonWriter` and `Command.Fingerprint` write it back unchanged · `Summary` "'scroll' step (needs a newer Augram)" |
| `UnknownStepType` | `Write` returns the original `params`; `Read` exists for the contract only (no Augram writes the key `unknown`); `Convert` is Same; `Execute` is Skipped "this Augram cannot run it (…); update Augram" with one Info line |

What is kept: the type key, `params`, `authoredOn` and `isActive` (those two are the envelope's, read as usual). Anything else a newer envelope might carry is not. The App shows the step read-only (`Components/Steps/Unknown/UnknownStepForm`); it can be deactivated, moved or deleted like any step.

**Never in the picker.** Category `StepCategory.Other`, like `Imported`.

**May reference:** `Diagnostics`, `Steps`. **Referenced by:** `StepRegistry.BuiltIn`, `Config/CommandStepJsonReader`, the App's step list (read-only form).
