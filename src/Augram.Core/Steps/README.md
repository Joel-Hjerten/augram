# Core/Steps

A **step** is one executable unit of a command (F5a: app group › command › step). This folder holds the contract every step type implements and one folder per type, each a vertical slice (ADR-0002 §4, requirement N3): record · type metadata and factory · executor. Nowhere in the codebase is there a `switch` over step kinds; everything that needs to know "what kinds exist" asks `StepRegistry`.

## The contract

| Type | Role |
|---|---|
| `IStep` | an immutable record holding one step's parameters: `Type` (its `IStepType`), `Summary` (one line for the command row: "Minimize window", "Wait 30 ms") |
| `IStepType` | what the rest of Augram knows about a kind of step: `Key` (stable, written to the config, never renamed), `DisplayName` and `Category` (the picker), `IsPlatformNeutral` (F8: no conversion or override needed), `CreateDefault()`, `Read(JsonObject)` / `Write(IStep)` (the parameters only), `Execute(IStep, StepExecutionContext)` |
| `StepCategory` | `System`, `Keyboard`, `Text`, `Run`, `Timing`, and `Other` (never offered by the picker; placeholders only) |
| `StepRegistry` | `BuiltIn`: the static registration list, one line per type; `All`, `Find(key)` (null when unknown), `Require(key)` (`StepFormatException` when unknown); duplicate keys are refused at construction |
| `StepExecutionContext` | what a step may touch while running: `Target` (window under the gesture start, already activated per A20, null when nothing was there), `Start`, `Windows` (`IWindowOperations`), `Input` (`IInputSimulator`), `Log`, `Cancellation`, `FocusMoved` |
| `StepResult` / `StepOutcome` | `Done`, `Skipped(reason)`, `Failed(reason)` |
| `StepFormatException` | a stored parameter cannot be read; the message names the member and is fit for the config notice |
| `StepParameters` | the shared reader every `Read` uses: `ReadEnum`, `ReadInt32(min, max)`, `ReadString` return null when the member is absent and throw a `StepFormatException` naming the member otherwise; `Required(name, when)`; `Expect<TStep>` for `Write`/`Execute` handed a foreign step |

## Shipped types

Picker order within a category is `BuiltIn` order.

| Key | Folder | Display name | Category | Neutral? | Summary examples |
|---|---|---|---|---|---|
| `windowOp` | `WindowOp/` | Window | System | yes | "Close window", "Maximize or restore", "Set size 1280×720", "Snap to left half" |
| `mediaKey` | `MediaKey/` | Media key | System | yes | "Volume up", "Mute", "Play/pause", "Next track" |
| `delay` | `Delay/` | Delay | Timing | yes | "Wait 30 ms" |
| `imported` | `Imported/` | Imported (not supported yet) | Other | yes (nothing to convert) | "SendAltDown (not supported yet)" |

Still to land in M2 (plan 0001 step 2): `Hotkey` (Keyboard, platform-bound: Ctrl ↔ Cmd conversion), `TypeText` (Text), `Run` (Run, platform-bound: paths), later `MouseClick`.

## Adding a type (the recipe)

One folder `Steps/<Name>/`, nothing outside it but one line:

1. `<Name>Step.cs`: a `sealed record` implementing `IStep`; `Type => <Name>StepType.Instance`; a `Summary`.
2. `<Name>StepType.cs`: a `sealed class` implementing `IStepType` with a private constructor and a static `Instance`. `Read` goes through `StepParameters` so every refusal names the member and missing members take defaults; `Write` emits exactly the members `Read` accepts, so a round trip is byte-stable; `Execute` delegates to the executor after `StepParameters.Expect<TStep>`.
3. `<Name>Executor.cs`: an `internal static class` with `Execute(<Name>Step, StepExecutionContext)`. It returns a `StepResult`, never throws for an expected failure, and logs one line with source `steps`.
4. `README.md`: purpose, the parameter shape, the execute branches, and, for a platform-bound type, the conversion rule and what each platform adapter does.
5. Tests in `tests/Augram.Core.Tests/Steps/`: default instance, read/write round trip including every parameter, every bad input, every execute branch. Fakes live in `Steps/Support/` there.
6. One line in `StepRegistry.BuiltIn`, in picker position.
7. The parameter form lands separately under `App/Components/Steps/<Name>/` (ADR-0002 §4) and is declared as a `SectionForm` tree (§5c); the App's step list finds it by the type's `Key`.

## Execution rules the executors rely on

These belong to the Engine's command executor (M2 step 3); every step is written against them.

- Steps of a command run **in order on one executor thread**, one command at a time (serialized task chain). A step may block that thread (Delay does); nothing on the hook or UI thread waits on it.
- `Skipped` **continues** the command (not supported on this platform, no target window, cancelled, placeholder). `Failed` **stops** it at that step. Both carry a reason the executor logs and the UI can show on the step.
- The **settle delay (A8)** is the executor's: applied once, before the first Keyboard or Text step, only when `FocusMoved` is true because activation per A20 changed the foreground window. Window operations, media keys and delays never wait for it, and no step applies it itself.
- The **target** (`StepExecutionContext.Target`) is resolved and activated once per command before the first step; a step never looks windows up.
- **Cancellation** (`StepExecutionContext.Cancellation`) is cooperative: a blocking step must wake on it and report `Skipped("cancelled")`; the executor then stops the chain.
- Only the executor thread calls `IInputSimulator` and `IWindowOperations` (A19: never the hook thread).

## Where the JSON goes

`Write()` returns the step's parameters only. The **Mapping** subsystem (`Core/Mapping`) owns the envelope it is stored in, per F8:

```json
{ "type": "windowOp", "authoredOn": "Windows", "isActive": true, "params": { "operation": "Minimize" }, "overrides": { "macos": { … } } }
```

`type` is the registry key, `authoredOn` a `HostPlatform`, `params` what `Write` produced, `overrides` per-platform parameter objects for platform-bound types (resolution order at run time: override for the current platform → conversion of the authored step). Mapping calls `StepRegistry.Require(type).Read(params)` on load; this folder never sees the envelope.

**May reference:** `Abstractions`, `Capture` (the start point), `Diagnostics`. **Referenced by:** `Mapping`, the Engine executor, the importer, the App's step list and picker.
