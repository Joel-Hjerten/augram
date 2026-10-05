# Core/Gestures

The gesture model: `GesturePoint` (raw X/Y), `GestureSample` (one training sample, an immutable raw point list), `Gesture` (id, name, active flag, samples) and `GestureId` (strongly typed Guid). Templates are **raw point lists** (CLAUDE.md invariant 5), resampled only at match time so precision stays adjustable.

Landed in M1 step 1. `GestureLibrary` (the store) and JSON persistence land in M1 step 2; nothing here knows about storage.

**May reference:** nothing. **Referenced by:** `Recognition`, the store, the importer, view models.
