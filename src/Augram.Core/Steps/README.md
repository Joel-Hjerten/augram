# Core/Steps

`IStep`, `IStepType`, `StepRegistry`, `StepExecutionContext`, plus one folder per step type (`Hotkey/`, `TypeText/`, `RunCommand/`, `MediaKey/`, `Delay/`). Each folder is a vertical slice: record, type metadata and factory, executor. Nowhere in the codebase is there a `switch` over step kinds (ADR-0002 §4, requirement N3).

Lands in M2 step 2. Nothing here yet.
