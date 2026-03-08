# DependentHiding Integration Test – Investigation & Fix Plan

## Test Overview

**Location:** `Content.IntegrationTests/Tests/Humanoid/HideableHumanoidLayersTest.cs`  
**Test:** `DependentHiding`  
**Purpose:** Verifies that when a mask hides the Snout layer, both the base Snout marking (VulpSnout) and the dependent SnoutCover marking (VulpSnoutNose) are hidden, and both become visible again when the mask is removed.

## Test Flow

1. Spawn player as `MobVulpkanin`
2. Apply markings: Head organ → SnoutCover layer with `VulpSnoutNose`
3. Equip gas mask (`ClothingMaskGas`)
4. Wait 20 ticks
5. **Assert:** `VulpSnout-snout` and `VulpSnoutNose-snout-nose` sprite layers are both **hidden**
6. Delete mask (de-equip)
7. Wait 20 ticks
8. **Assert:** Both layers are **visible** again

## Architecture

### Key Components

| Component | Location | Role |
|-----------|----------|------|
| `HideableHumanoidLayersComponent` | Player | Tracks which layers are hidden and by which slots |
| `VisualOrganMarkingsComponent` | OrganVulpkaninHead | Holds `HideableLayers` and `DependentHidingLayers` |
| `HumanoidLayerVisibilityChangedEvent` | Shared | Raised when a layer’s visibility changes |

### Event Flow

1. **Equip mask** → `HideLayerClothingSystem.OnHideGotEquipped` → `SetLayerOcclusion(Snout, hidden: true)`
2. **Server:** `HideableHumanoidLayersComponent.HiddenLayers[Snout]` updated → state replicated
3. **Client:** `AfterAutoHandleStateEvent` → `HideableHumanoidLayersSystem.UpdateSprite` → raises `HumanoidLayerVisibilityChangedEvent(Snout, visible: false)`
4. **Client:** `BodySystem` relays event to all organs via `BodyRelayedEvent<HumanoidLayerVisibilityChangedEvent>`
5. **Client:** `VisualBodySystem.OnMarkingsChangedVisibility` on OrganVulpkaninHead:
   - Skips if `args.Args.Layer` (Snout) is not in `HideableLayers` ✓ (Snout is in HideableLayers)
   - For each marking: process if `proto.BodyPart == Snout` OR `DependentHidingLayers[Snout].Contains(proto.BodyPart)`
   - Sets sprite visibility via `_sprite.LayerSetVisible(args.Body.Owner, index, args.Args.Visible)`

### Vulpkanin Configuration

From `Resources/Prototypes/Body/Species/vulpkanin.yml` (OrganVulpkaninHead):

```yaml
hideableLayers:
  - enum.HumanoidVisualLayers.Snout
  - enum.HumanoidVisualLayers.HeadTop
  # ...
dependentHidingLayers:
  enum.HumanoidVisualLayers.Snout:
  - enum.HumanoidVisualLayers.SnoutCover
```

So when Snout is hidden, SnoutCover is also hidden.

## Potential Failure Points

### 1. **Client–Server Sync Timing**

- **Risk:** Client asserts before state has replicated.
- **Mitigation:** Increase `RunTicks(20)` if needed, or add a retry/assertion helper that waits for expected visibility.

### 2. **Layer Naming / Existence**

- **Risk:** `LayerMapGet` throws if the layer does not exist.
- **Current:** Layer IDs are `{MarkingId}-{RsiState}` (e.g. `VulpSnout-snout`, `VulpSnoutNose-snout-nose`).
- **Mitigation:** Use `LayerMapTryGet` in the test and fail with a clear message if the layer is missing.

### 3. **ApplyMarkings Key**

- **Risk:** Wrong organ category key.
- **Current:** Test uses `["Head"]`; OrganVulpkaninHead has `category: Head`.
- **Status:** Correct.

### 4. **HideableLayers Check**

- **Risk:** Organ does not process the event if the layer is not in `HideableLayers`.
- **Current:** OrganVulpkaninHead has Snout in `HideableLayers`.
- **Status:** Correct.

### 5. **DependentHidingLayers Semantics**

- **Risk:** Wrong direction or missing mapping.
- **Current:** `DependentHidingLayers[Snout] = [SnoutCover]` means “when Snout is hidden, also hide SnoutCover”.
- **Status:** Correct.

### 6. **Server Event Semantics (Shared Code)**

- **Observation:** In `SharedHideableHumanoidLayersSystem.SetLayerOcclusion`:
  ```csharp
  var evt = new HumanoidLayerVisibilityChangedEvent(layer, ent.Comp.HiddenLayers.ContainsKey(layer));
  ```
  When the layer is hidden, `ContainsKey` is true, so the event passes `Visible: true`. That is inverted for sprite visibility.
- **Impact:** The server raises this event, but `VisualBodySystem` is client-only. The client drives visibility via `UpdateSprite` on state sync, which uses the correct `Visible` values. So this does not affect the test.

## Fix Plan

### Option A: Test Robustness (Recommended First)

1. **Use `LayerMapTryGet` in the test**  
   Replace `LayerMapGet` with `LayerMapTryGet` and fail with a clear message if the layer is missing (e.g. marking not applied or wrong ID).

2. **Increase wait time if flaky**  
   If the test is flaky, increase `RunTicks(20)` to `RunTicks(30)` or `RunTicks(40)` to allow more time for replication.

3. **Add a small retry for assertions**  
   If needed, wrap visibility assertions in a short retry loop (e.g. 5 attempts with a few ticks between) to tolerate timing variance.

### Option B: Code Fixes (If Test Still Fails)

1. **Fix server event semantics (optional)**  
   In `SharedHideableHumanoidLayersSystem.SetLayerOcclusion`, change:
   ```csharp
   var evt = new HumanoidLayerVisibilityChangedEvent(layer, !ent.Comp.HiddenLayers.ContainsKey(layer));
   ```
   so that `Visible` matches actual visibility. This keeps shared code consistent even if the client does not rely on it.

2. **Verify `GetAllOrgans` includes the head**  
   Ensure `BodySystem.GetAllOrgans` returns OrganVulpkaninHead so it receives the relayed event. Current implementation includes direct body organs (torso, head, arms, legs) and their nested organs.

3. **Add logging for debugging**  
   Temporary logging in `OnMarkingsChangedVisibility` for:
   - Whether the organ receives the event
   - Which markings are processed
   - Final visibility values

### Option C: Test Simplification

1. **Use `LayerMapTryGet` and fail clearly**  
   Example:
   ```csharp
   Assert.That(spriteSystem.LayerMapTryGet(CPlayer, "VulpSnout-snout", out snoutIndex, true), Is.True,
       "VulpSnout-snout layer not found - marking may not have been applied");
   ```

2. **Assert layer existence before visibility**  
   Add a preliminary check that both layers exist before asserting visibility.

## Verification

- Run the test multiple times to check for flakiness:
  ```bash
  dotnet test Content.IntegrationTests/Content.IntegrationTests.csproj --filter "FullyQualifiedName~DependentHiding" --no-build
  ```
- If it fails, capture the exact assertion message and stack trace.
- If layers are missing, verify that `ApplyMarkings` is called correctly and that the Head organ receives the markings.

## Summary

The design and configuration for dependent hiding appear correct. The most likely causes of failure are:

1. **Timing:** Client asserts before state replication.
2. **Missing layers:** Markings not applied or wrong layer IDs.
3. **Environment:** Different behavior in CI or other environments.

Recommended order of actions:

1. Make the test more robust (Option A).
2. If it still fails, add logging and verify event flow (Option B).
3. Optionally fix the server event semantics for consistency (Option B.1).
