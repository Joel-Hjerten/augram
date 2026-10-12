# Steps

One parameter form per step type (ADR-0002 §4): `<Name>/<Name>StepForm`, an `IStepForm` found by scan (`StepFormRegistry`; `StepFormRegistryTests` fails while a built-in type has none). `Build(current, changed)` returns a `SectionForm` over a declared `FormScreen`; a form that adapts to its command takes a `StepFormContext` (its trigger here). How to add one: the App README, "How to add… A step type's form".

| Type | Job |
|---|---|
| `StepFormState<TStep>` | The value a form edits and its one edit path: `Emit` normalises (optional), drops an edit that changes nothing, tells `Accepted`, calls `changed`, refreshes what `Refreshed`/`OnEdit` registered. `Bind` makes a field's binding (its inspector name read from the getter). Delay, Media key, Type text, Run, Scroll, Hotkey and Remap use it. |
| `StepFormRegistry`, `IStepForm`, `StepFormContext` | The scan, the contract, the command context. |

Key and modifier editors come from `Capture/HotkeyChoices`. Not on `StepFormState` yet: HDR (it emits an edit that changes nothing, as Delay and Media key did), Window and Display mode (they re-declare their form when it changes shape), Open app (an observable form state).
