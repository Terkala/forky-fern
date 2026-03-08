# EntityTest.SpawnAndDeleteAllEntitiesOnDifferentMaps — Failure Analysis

## Executive Summary

The integration test `SpawnAndDeleteAllEntitiesOnDifferentMaps` fails with:

```
System.Exception : DisposeAsync: Unexpected state. Pair: 6. State: CleanDisposed.
```

This error is a **secondary symptom** that masks the real failure. The root cause is an exception thrown earlier during `OnCleanDispose()`, which leaves the pair in `CleanDisposed` state. When the `await using` block runs `DisposeAsync()`, it does not handle this state and throws the reported error, often hiding the original exception.

---

## 1. Test Overview

**Location:** `Content.IntegrationTests/Tests/EntityTest.cs` (lines 50–111)

**Purpose:** Stress test that spawns every non-abstract entity prototype on separate maps, runs 450 ticks, then deletes all entities and returns the pair cleanly.

**Flow:**
1. Get a pooled server-client pair with `PoolSettings { Dirty = true }` (client disconnected)
2. For each entity prototype (excluding MapGrid, RoomFill, etc.):
   - Create a new map
   - Create a grid with one tile
   - Spawn one entity of that prototype
3. Run 450 ticks (~15 seconds)
4. Delete all entities via `entityMan.DeleteEntity(uid)` for every entity
5. Assert `entityMan.EntityCount == 0`
6. Call `await pair.CleanReturnAsync()`

---

## 2. Failure Flow

### 2.1 State Machine

`TestPair` uses a simple state machine (`PairState`):

| State        | Value | Meaning                                      |
|-------------|-------|----------------------------------------------|
| `Ready`     | 0     | Pair is in pool, available for reuse        |
| `InUse`     | 1     | Pair is borrowed by a test                   |
| `CleanDisposed` | 2 | Clean return in progress (intermediate)      |
| `Dead`      | 3     | Pair has been killed                         |

### 2.2 CleanReturnAsync Flow

```csharp
// TestPair.Recycle.cs, lines 86-97
public async ValueTask CleanReturnAsync()
{
    if (State != PairState.InUse)
        throw new Exception(...);

    await TestOut.WriteLineAsync($"{nameof(CleanReturnAsync)}: Return of pair {Id} started");
    State = PairState.CleanDisposed;   // ← State set here
    await OnCleanDispose();            // ← If this throws, State stays CleanDisposed
    DebugTools.Assert(State is PairState.Dead or PairState.Ready);
    Manager.Return(this);
    ClearContext();
}
```

### 2.3 OnCleanDispose Flow

```csharp
// TestPair.Recycle.cs, lines 43-85
private async Task OnCleanDispose()
{
    await Server.WaitIdleAsync();
    await Client.WaitIdleAsync();
    await Cleanup();
    await Server.Cleanup();
    await Client.Cleanup();
    await RevertModifiedCvars();
    // ... usage time logging ...
    await ReallyBeIdle();              // 25 ticks of server + client
    // ... alive checks, runtime log checks ...
    State = PairState.Ready;           // ← Only reached if nothing throws
}
```

### 2.4 DisposeAsync Flow

```csharp
// TestPair.Recycle.cs, lines 99-116
public async ValueTask DisposeAsync()
{
    switch (State)
    {
        case PairState.Dead:
        case PairState.Ready:
            break;                     // OK — do nothing
        case PairState.InUse:
            await OnDirtyDispose();
            Manager.Return(this);
            break;
        default:                       // CleanDisposed falls here!
            throw new Exception($"{nameof(DisposeAsync)}: Unexpected state. Pair: {Id}. State: {State}.");
    }
}
```

`DisposeAsync` does not handle `CleanDisposed`. That state is meant to be transient inside `CleanReturnAsync`.

---

## 3. Root Cause Analysis

### 3.1 Exception Masking

When `OnCleanDispose()` throws:

1. The exception propagates out of `CleanReturnAsync()`.
2. `State` remains `CleanDisposed` (no `try/finally` to reset it).
3. The test’s `await using` runs its `finally` block.
4. `finally` calls `await pair.DisposeAsync()`.
5. `DisposeAsync` sees `State == CleanDisposed` and throws `"Unexpected state"`.

In C#, if both the `try` and `finally` throw, the `finally` exception can become the one reported, and the original exception from `OnCleanDispose` may be lost. That is why the test reports `DisposeAsync: Unexpected state` instead of the real failure.

### 3.2 Likely Failure Points in OnCleanDispose

Given the test’s behavior, plausible failure points:

| Step | Possible failure |
|------|-------------------|
| `Server.WaitIdleAsync()` | Server threw during earlier ticks (e.g., after deleting all entities). |
| `Client.WaitIdleAsync()` | Client threw (e.g., after entity flush). |
| `ReallyBeIdle()` | Runs 25 ticks on both server and client; any system accessing deleted entities can throw. |
| `IRuntimeLog.ExceptionCount > 0` | Server or client logged an exception during the test. |
| `Client.IsAlive == false` / `Server.IsAlive == false` | Server or client died during the test. |

### 3.3 Test-Specific Stress

The test is unusually heavy:

1. **Many maps:** One map per entity prototype (hundreds).
2. **All entities deleted:** Every entity, including maps and grids, is removed.
3. **Zero-entity state:** After deletion, the server has no entities.
4. **450 ticks:** Long run with game rules, spawners, and other systems active.

Systems may assume that certain entities (e.g., maps, grids, game rules) still exist. After deleting everything, systems can:

- Access deleted entities.
- Assume a default map or grid exists.
- Trigger null references or invalid-entity access.

These can surface during `WaitIdleAsync()` or `ReallyBeIdle()` when the server processes ticks with zero entities.

### 3.4 Pool Reuse and Ordering

Test output shows:

```
CleanReturnAsync: Return of pair 6 started
CleanReturnAsync: Test borrowed pair 6 for 99935.2551 ms
Failed TryStopNukeOpsFromConstantlyFailing [645 ms]
```

`TryStopNukeOpsFromConstantlyFailing` fails during recycling with:

```
Entity 106305 is not valid. (Parameter 'uid')
```

This happens in `SpeedModifierContactsSystem.Update` → `MovementSpeedModifierSystem.RefreshMovementSpeedModifiers` when adding a component to an entity that was already deleted during `FlushEntities()` in `Recycle`.

So there are two related issues:

1. **SpawnAndDeleteAllEntitiesOnDifferentMaps:** Fails during `CleanReturnAsync` / `OnCleanDispose`, with the real error masked by `DisposeAsync: Unexpected state`.
2. **TryStopNukeOpsFromConstantlyFailing:** Fails during recycling because systems still reference entities that were flushed.

---

## 4. Design Issues

### 4.1 CleanDisposed Not Handled in DisposeAsync

`DisposeAsync` treats `CleanDisposed` as invalid. When `CleanReturnAsync` fails partway through, the pair is left in `CleanDisposed` and `DisposeAsync` throws instead of cleaning up.

**Recommendation:** Handle `CleanDisposed` in `DisposeAsync`, e.g.:

```csharp
case PairState.CleanDisposed:
    // CleanReturnAsync failed partway; treat as dirty dispose
    await OnDirtyDispose();
    Manager.Return(this);
    ClearContext();
    break;
```

Or at least treat it as a non-fatal state and avoid throwing, so the original exception is not masked.

### 4.2 No try/finally in CleanReturnAsync

If `OnCleanDispose()` throws, `State` is never reset. A `try/finally` could ensure `State` is updated or the pair is marked for dirty disposal on failure.

### 4.3 Exception Preservation

When `DisposeAsync` runs after a failed `CleanReturnAsync`, the original exception should be preserved (e.g., as `InnerException`) so the real failure is visible in test output.

---

## 5. Recommendations

### 5.1 Short-Term

1. **Handle `CleanDisposed` in `DisposeAsync`** so it does not throw and does not mask the original error.
2. **Add `try/finally` in `CleanReturnAsync`** to reset state or perform dirty disposal on failure.
3. **Preserve the original exception** when `DisposeAsync` runs after a failed clean return.

### 5.2 Medium-Term

1. **Add logging** in `OnCleanDispose` to record which step fails.
2. **Run the test in isolation** to capture the real exception before it is masked.
3. **Review systems** that run during `ReallyBeIdle` for invalid-entity access when the world is empty.

### 5.3 Long-Term

1. **Harden systems** against zero-entity or partially-flushed worlds (null checks, `EntityExists`, etc.).
2. **Consider isolating this test** (e.g., `MustBeNew` or `Destructive`) so it does not reuse a pair that may be in a bad state.
3. **Add a timeout or guard** for tests that delete all entities to avoid long runs in invalid states.

---

## 6. Files Involved

| File | Relevance |
|------|-----------|
| `Content.IntegrationTests/Tests/EntityTest.cs` | Test implementation |
| `RobustToolbox/Robust.UnitTesting/Pool/TestPair.Recycle.cs` | `CleanReturnAsync`, `OnCleanDispose`, `DisposeAsync` |
| `RobustToolbox/Robust.UnitTesting/Pool/TestPair.cs` | State machine, `PairState` |
| `RobustToolbox/Robust.UnitTesting/Pool/ITestPair.cs` | `PairState` enum |
| `Content.IntegrationTests/Pair/TestPair.Recycle.cs` | Content-specific `Recycle` (round restart, entity flush) |
| `Content.Shared/Movement/Systems/SpeedModifierContactsSystem.cs` | System that fails during recycling (calls MovementSpeedModifierSystem) |
| `Content.Shared/Movement/Systems/MovementSpeedModifierSystem.cs` | Adds component to invalid entity during RefreshMovementSpeedModifiers |

---

## 7. Related Documentation

- **INTEGRATION_TEST_FIX_PLAN.md** — Lists `SpawnAndDeleteAllEntitiesOnDifferentMaps` as a known failure (Entity/Pool, Medium complexity) and recommends addressing it after fixing `MovementSpeedModifierSystem` (BananaSlipTest). The `MovementSpeedModifierSystem` fix (guarding against terminating entities) may also help this test.

---

## 8. Appendix: PairState Enum

```csharp
// RobustToolbox/Robust.UnitTesting/Pool/ITestPair.cs
public enum PairState : byte
{
    Ready = 0,
    InUse = 1,
    CleanDisposed = 2,
    Dead = 3,
}
```
