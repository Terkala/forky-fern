# Integration Test Fix Plan

This document provides a structured plan with to-do lists for each failing integration test. Complete all items before deploying code.

---

## 1. DeconstructProtolathe

**File:** `Content.IntegrationTests/Tests/Construction/Interaction/MachineConstruction.cs`  
**Error:** `Missing entity/stack: Manipulator, quantity: 3`  
**Location:** `MachineConstruction.cs:42` (AssertEntityLookup)

### Root Cause
The test expects 4 Manipulators (`Manipulator1` = MicroManipulatorStockPart) after full protolathe deconstruction. Only 1 is found. The construction graph's machine deconstruction path may not be returning all machine parts (board + stock parts) correctly, or the Protolathe board's `stackRequirements` (Manipulator: 4) may not match what is actually dropped.

### To-Do
- [ ] Inspect the construction graph (`machine.yml`) machine→machineFrame edge: `MachineDeconstructedEvent` and `EmptyAllContainers` behavior
- [ ] Verify `MachineFrameRegenerateProgress` and how machine parts are restored when prying the board
- [ ] Check if a fork-specific prototype override (e.g. `_Funkystation`) changes Protolathe board requirements
- [ ] Confirm `AssertEntityLookup` uses correct stack/prototype (Manipulator vs MicroManipulatorStockPart)
- [ ] Either fix the construction/deconstruction logic to yield 4 Manipulators, or update the test's expected output if the design has changed

---

## 2. CyberArmGun_EjectMagazineVerb_RelaysToBlockingEntity

**File:** `Content.IntegrationTests/Tests/Cybernetics/CyberArmGunFixesIntegrationTest.cs`  
**Error:** `Assert.That(handsSystem.TryGetActiveItem(user, out var held), Is.True)` — Expected: True, But was: False  
**Location:** `CyberArmGunFixesIntegrationTest.cs:305`

### Root Cause
After `EmptyHandActivateEvent` and `CyberArmSelectRequestMessage` (selecting the gun), the test expects the user to have an active held item (the virtual item for the cyber arm gun). `TryGetActiveItem` returns false — the gun/virtual item is not in hand.

### To-Do
- [ ] Trace why `EmptyHandActivateEvent` + `CyberArmSelectRequestMessage` does not result in the gun being "in hand" as a virtual item
- [ ] Check if `CyberArmSelectSystem` or related logic requires an active hand or specific conditions
- [ ] Verify `ReplaceArmWithCyberArm` correctly sets up a cyber arm that can hold items
- [ ] Add ticks or wait logic if the virtual item spawn is delayed
- [ ] Consider whether the test's hand/activation setup matches real gameplay (e.g. which hand is used)

---

## 3. CyberArmGun_ResolvesInTryGetGun_AndCanShoot

**File:** `Content.IntegrationTests/Tests/Cybernetics/CyberArmGunFixesIntegrationTest.cs`  
**Error:** `Assert.That(userInterface.IsUiOpen(cyberArm, CyberArmSelectUiKey.Key, user), Is.True)` — Expected: True, But was: False  
**Location:** `CyberArmGunFixesIntegrationTest.cs:148`

### Root Cause
The Cyber Arm Select UI is expected to be open after activation, but it is not. This may be a timing/ordering issue or the activation path that opens the UI is not being triggered.

### To-Do
- [ ] Identify what opens `CyberArmSelectUiKey` — likely `EmptyHandActivateEvent` on a cyber arm with items
- [ ] Ensure the test triggers the same code path (e.g. correct hand, correct target)
- [ ] Add `RunTicksSync` or `WaitIdle` if the UI opens asynchronously
- [ ] Check if the UI is opened client-side only and the test runs server-side assertions

---

## 4. CyberArmSelect_SpawnsVirtualItemWithUnremoveable_WhenItemSelected

**File:** `Content.IntegrationTests/Tests/Cybernetics/CyberArmSelectIntegrationTest.cs`  
**Error:** `Cyber arm select UI should be open` — `IsUiOpen(cyberArm, CyberArmSelectUiKey.Key, user)` is False  
**Location:** `CyberArmSelectIntegrationTest.cs:142`

### Root Cause
Same family as #3: Cyber Arm Select UI is not open when the test expects it. Likely shared cause with EmptyHandActivate / UI opening logic.

### To-Do
- [ ] Reuse findings from #3 — fix the UI opening path for cyber arm select
- [ ] Ensure the test's setup (user with cyber arm, items in storage) matches conditions for UI to open
- [ ] Add timing/wait if UI opens after a delay

---

## 5. CyberArmGun_VirtualItemInvalidated_WhenRemovedFromStorage

**File:** `Content.IntegrationTests/Tests/Cybernetics/CyberArmGunFixesIntegrationTest.cs`  
**Error:** `Assert.That(handsSystem.TryGetActiveItem(user, out var held), Is.True)` — Expected: True, But was: False  
**Location:** `CyberArmGunFixesIntegrationTest.cs:226`

### Root Cause
After selecting the gun from cyber arm storage and running the test flow, the user is expected to have an active held item (virtual item). They do not. The test validates that when the gun is removed from storage, the virtual item is invalidated — but the failure occurs earlier when expecting the virtual item to exist.

### To-Do
- [ ] Confirm the gun is successfully selected and a virtual item is spawned before the "removed from storage" step
- [ ] Check if virtual item creation requires the UI to be open or specific message ordering
- [ ] Align with fixes for #2 and #3 — virtual item and UI behavior
- [ ] Increase `RunTicksSync` if virtual item spawn is delayed

---

## 6. EmptyHandActivate_OpensCyberArmSelectUI_WhenCyberArmHasItems

**File:** `Content.IntegrationTests/Tests/Cybernetics/CyberArmSelectIntegrationTest.cs`  
**Error:** `EmptyHandActivateEvent should be handled` — `Assert.That(ev.Handled, Is.True)` — Expected: True, But was: False  
**Location:** `CyberArmSelectIntegrationTest.cs:81`

### Root Cause
`EmptyHandActivateEvent` is raised but not handled. The system that should open the Cyber Arm Select UI when activating with an empty hand (and a cyber arm with items) is not marking the event as handled.

### To-Do
- [ ] Find the subscriber for `EmptyHandActivateEvent` that opens Cyber Arm Select UI
- [ ] Verify the subscriber's conditions: user has cyber arm, cyber arm has items, correct hand
- [ ] Check if the event is raised on the correct entity (user vs cyber arm)
- [ ] Fix the handler or test setup so `ev.Handled` is true when the UI opens

---

## 7. SpawnAndDeleteAllEntitiesOnDifferentMaps

**File:** `Content.IntegrationTests/Tests/EntityTest.cs`  
**Error:** `DisposeAsync: Unexpected state. Pair: 9. State: CleanDisposed`  
**Location:** `EntityTest.cs:110`, `TestPair.Recycle.cs:113`

### Root Cause
The test uses `CleanReturnAsync()` but the pair ends up in `CleanDisposed` state, causing `DisposeAsync` to fail. This usually happens when the test or teardown triggers a clean return (e.g. `TearDownInternal` or map deletion) while the test also explicitly calls `CleanReturnAsync`, or when the pair is returned twice.

### To-Do
- [ ] Review `EntityTest.cs` around line 110 — ensure only one return path (CleanReturn vs Dirty)
- [ ] Check `TearDownInternal` in `InteractionTest` (line 296) — it deletes the map and may trigger `CleanReturnAsync`
- [ ] Ensure `SpawnAndDeleteAllEntitiesOnDifferentMaps` does not conflict with base teardown (e.g. avoid double return)
- [ ] Consider using `Dirty = true` if the test's entity deletion is incompatible with clean recycling

---

## 8. LayerState_ComputedFromConfig_NotHardcoded

**File:** `Content.IntegrationTests/Tests/Medical/DynamicSurgeryConfigIntegrationTest.cs`  
**Error:** `RetractSkin: doafter-failed` — `Assert.That(reqEv.Valid, Is.True)` — Expected: True, But was: False  
**Location:** `DynamicSurgeryConfigIntegrationTest.cs:261`

### Root Cause
A surgery step (RetractSkin) returns `reqEv.Valid == false` with reason "doafter-failed". The DoAfter for the step is failing or being rejected, so the surgery request is invalid.

### To-Do
- [ ] Increase `RunTicksSync` after prior steps (CutBone, etc.) so DoAfters complete before RetractSkin
- [ ] Verify surgery config prerequisites — RetractSkin may require prior steps to be fully completed
- [ ] Check if the correct tool is in hand and the patient/target state is valid
- [ ] Inspect `SurgerySystem` / `SurgeryRequestEvent` validation for "doafter-failed"

---

## 9. ReattachedLimb_CloseIncision_RetractSkinAvailableAgain

**File:** `Content.IntegrationTests/Tests/Medical/ReattachedLimbSurgeryIntegrationTest.cs`  
**Error:** `Expected at least 1 DoAfter(s) to be active (action may have been rejected)`  
**Location:** `InteractionTest.Helpers.cs:589` (AwaitDoAfters), `ReattachedLimbSurgeryIntegrationTest.cs:164`

### Root Cause
`AwaitDoAfters(minExpected: 1)` expects at least one active DoAfter after requesting RetractSkin (or similar step). None are active — the action was likely rejected.

### To-Do
- [ ] Verify the surgery layer state before RetractSkin — CloseIncision may need to complete first
- [ ] Ensure the correct tool is in hand and the target body part is in the right state
- [ ] Check surgery config: prerequisites for RetractSkin after reattachment
- [ ] Add more ticks before the RetractSkin request if prior DoAfters need time to finish

---

## 10. SurgeryRequestBuiMessage_RemoveOrgan_ThenInsertOrgan_Completes

**File:** `Content.IntegrationTests/Tests/Medical/OrganRemovalSurgeryIntegrationTest.cs`  
**Error:** `Heart must be in hand before InsertOrgan` — `Assert.That(HandSys.IsHolding(SPlayer, heart), Is.True)` — Expected: True, But was: False  
**Location:** `OrganRemovalSurgeryIntegrationTest.cs:234`

### Root Cause
After RemoveOrgan, the heart is not in the surgeon's hand when InsertOrgan is requested. The test already drops cautery and calls `TryPickupAnyHand(heart)`, but the assertion still fails — either the pickup fails, or something drops the heart between pickup and the assertion.

### To-Do
- [ ] Ensure both hands are free (or one free) before RemoveOrgan — drop all tools except analyzer if needed
- [ ] Add an assertion after `TryPickupAnyHand` to fail fast if pickup fails
- [ ] Run extra ticks between pickup and InsertOrgan to allow server sync
- [ ] Check if `SurgerySystem` auto-pickup (line ~671) puts the heart in a different slot — ensure we're checking the right hand
- [ ] Consider using `OrganInsertRequestEvent` as fallback when BUI path doesn't complete (already partially implemented)

---

## 11. DetachLimb_OnLeg_DetachesLegAndFootSeparately

**File:** `Content.IntegrationTests/Tests/Medical/SurgeryFixesIntegrationTest.cs`  
**Error:** `Expected at least 1 DoAfter(s) to be active (action may have been rejected)`  
**Location:** `InteractionTest.Helpers.cs:589`, `SurgeryFixesIntegrationTest.cs:348`

### Root Cause
The test expects a DoAfter to start for a limb detachment step (likely DetachLimb on the leg). No DoAfter is active — the request may be rejected due to invalid state, wrong tool, or unmet prerequisites.

### To-Do
- [ ] Verify the surgery layer and body part state before the DetachLimb request
- [ ] Ensure the correct tool (e.g. saw, scalpel) is in hand
- [ ] Check surgery config for DetachLimb prerequisites on legs
- [ ] Add ticks after prior steps so previous DoAfters complete
- [ ] Inspect why the action is rejected — add logging or breakpoint at DoAfter start

---

## 12. BananaSlipTest

**File:** (likely in `Content.IntegrationTests/Tests/`)  
**Error:** `Attempted to add a MovementSpeedModifierComponent component to an entity while it is terminating`  
**Location:** `MovementSpeedModifierSystem.cs:127` (RefreshMovementSpeedModifiers), called from `OnStand` (line 68)

### Root Cause
During entity shutdown/recursive delete (e.g. map deletion in teardown), `StoodEvent` is raised. `MovementSpeedModifierSystem.OnStand` calls `RefreshMovementSpeedModifiers`, which tries to add `MovementSpeedModifierComponent` to an entity that is already terminating.

### To-Do
- [ ] In `MovementSpeedModifierSystem.OnStand`, check if the entity is terminating before adding/modifying components
- [ ] In `RefreshMovementSpeedModifiers`, add `MetaDataComponent.LifeStage` or `EntityManager.IsTerminating(uid)` guard
- [ ] Alternatively, in `SharedStunSystem.OnKnockShutdown` (which calls `Stand`), avoid raising `StoodEvent` if the entity is terminating
- [ ] Ensure the fix does not break normal standing behavior

---

## Summary Table

| # | Test Name | Category | Est. Complexity |
|---|-----------|----------|-----------------|
| 1 | DeconstructProtolathe | Construction | Medium |
| 2 | CyberArmGun_EjectMagazineVerb_RelaysToBlockingEntity | Cybernetics | Medium |
| 3 | CyberArmGun_ResolvesInTryGetGun_AndCanShoot | Cybernetics | Medium |
| 4 | CyberArmSelect_SpawnsVirtualItemWithUnremoveable_WhenItemSelected | Cybernetics | Medium |
| 5 | CyberArmGun_VirtualItemInvalidated_WhenRemovedFromStorage | Cybernetics | Medium |
| 6 | EmptyHandActivate_OpensCyberArmSelectUI_WhenCyberArmHasItems | Cybernetics | Medium |
| 7 | SpawnAndDeleteAllEntitiesOnDifferentMaps | Entity/Pool | Medium |
| 8 | LayerState_ComputedFromConfig_NotHardcoded | Medical/Surgery | Medium |
| 9 | ReattachedLimb_CloseIncision_RetractSkinAvailableAgain | Medical/Surgery | Medium |
| 10 | SurgeryRequestBuiMessage_RemoveOrgan_ThenInsertOrgan_Completes | Medical/Surgery | Medium |
| 11 | DetachLimb_OnLeg_DetachesLegAndFootSeparately | Medical/Surgery | Medium |
| 12 | BananaSlipTest | Movement/Shutdown | Low |

---

## Recommended Order

1. **BananaSlipTest** — Single, localized fix in `MovementSpeedModifierSystem`; unblocks teardown.
2. **SpawnAndDeleteAllEntitiesOnDifferentMaps** — Pool/teardown fix; may reduce noise in other tests.
3. **DeconstructProtolathe** — Construction logic; independent.
4. **EmptyHandActivate_OpensCyberArmSelectUI_WhenCyberArmHasItems** — Core cyber arm UI; may fix #3, #4, #5, #6.
5. **Cyber arm gun tests (#2, #3, #5)** — After #6 is fixed.
6. **Medical/Surgery tests (#8, #9, #10, #11)** — May share DoAfter/timing fixes.

---

## Final Checklist Before Deploy

- [ ] All 12 tests pass locally
- [ ] Full integration test suite run (`dotnet test Content.IntegrationTests`)
- [ ] No new linter/analyzer warnings
- [ ] Non-trivial changes documented in commit message or PR
