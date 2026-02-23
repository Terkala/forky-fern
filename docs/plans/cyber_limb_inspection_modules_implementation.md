# Cyber-Limb Inspection: Installed Modules

## Overview

This plan implements the "List of installed modules" feature for Cyber-Limb Inspection, as specified in the design document (Integrity-Based Medical and Cybernetics System). The inspection system already shows service time and efficiency when the examiner wears diagnostic goggles; this stage adds enumeration of items stored in cyber limbs.

This stage is self-contained and testable. It does not introduce Battery, CyberLimbModuleSystem, or any later design features.

## Design Document Reference

From `docs/design-proposals/integrity_medical_and_cybernetics.md`:

> **Cyber-Limb Inspection:**
> - Requires diagnostic goggles (ShowHealthBarsComponent with Silicon damage container)
> - Examine verb shows: battery lifespan, service time, efficiency %, **list of installed modules**

The design doc also states (Implementation Status):

> - **CyberLimbInspectionSystem** (Shared): ExaminedEvent on CyberLimbStatsComponent; examiner null check; goggle check via `TryGetSlotEntity(examiner, "eyes")` and ShowHealthBarsComponent with Silicon on equipped goggles; push service time, efficiency

Battery and module-type parsing are explicitly out of scope for this stage.

## Scope

**In scope:**
- Extend `CyberLimbInspectionSystem` to enumerate storage contents of all cyber limbs on the examined body
- Display stored item names in the examine text (as "installed modules")
- Add locale string for the modules list
- Add integration test verifying the feature

**Out of scope:**
- Battery status (design doc: "no Battery yet")
- Module type classification (Battery, Matter Bin, Manipulator, etc.) – requires CyberLimbModuleSystem
- Any changes to storage behavior, maintenance, or other systems

## Implementation Steps

### 1. Extend CyberLimbInspectionSystem

**File:** `Content.Shared/Cybernetics/Systems/CyberLimbInspectionSystem.cs`

- Add dependency on `BodySystem` to enumerate organs
- Add dependency on `SharedContainerSystem` or `StorageComponent` access to read container contents
- After pushing service time and efficiency, iterate over all organs on the body via `BodySystem.GetAllOrgans(body)`
- For each organ with `CyberLimbComponent` and `StorageComponent`, enumerate `StorageComponent.Container.ContainedEntities`
- For each contained entity, resolve display name (e.g. via `MetaDataComponent` or `Loc.GetString` with entity prototype name)
- If any modules exist, push a single markup line: `cyber-limb-inspection-modules` with the comma-separated list

**Technical notes:**
- `CyberLimbStatsComponent` is on the body; the examined entity is the body
- Cyber limbs are organs in `BodyComponent.Organs`; each may have `StorageComponent`
- Storage contents are readable via `StorageComponent.Container.ContainedEntities` regardless of maintenance panel state (we are not opening UI)
- Use `MetaDataComponent.EntityName` or `Name(uid)` for display names

### 2. Add Locale String

**File:** `Resources/Locale/en-US/cybernetics/cybernetics-maintenance.ftl`

Add:

```
cyber-limb-inspection-modules = Installed modules: {$modules}
cyber-limb-inspection-no-modules = No modules installed
```

When the list is empty, either omit the line (current behavior) or show "No modules installed". Prefer omitting for consistency with other optional examine details.

### 3. Integration Test

**File:** `Content.IntegrationTests/Tests/Cybernetics/CyberLimbInspectionIntegrationTest.cs`

Add test: `WithDiagnosticGoggles_ExamineShowsInstalledModules`

**Steps:**
1. Spawn patient (MobHuman) and examiner (MobHuman)
2. Replace patient's left arm with cyber arm (detach organic arm, spawn OrganCyberArmLeft, insert into body)
3. Before attaching: get the cyber arm entity, use `SharedStorageSystem.Insert` to insert a known item (e.g. "Screwdriver") into the cyber arm's storage
4. Equip examiner with diagnostic goggles (ClothingEyesHudDiagnostic in eyes slot)
5. Raise `ExaminedEvent` on patient with examiner
6. Assert examine text contains the item name (e.g. "Screwdriver") and "Installed modules" or "modules"

**Helper:** Reuse `ReplaceArmWithCyberArm` pattern from existing tests, but perform storage insert while the cyber arm is still detached (before inserting into body). The test helper in CyberLimbStorageIntegrationTest inserts after spawn – we need: spawn cyber arm, insert item, then insert cyber arm into body.

**Alternative flow:** Spawn cyber arm at coords, insert screwdriver into storage, then use OrganInsertRequestEvent or container insert to attach to body. The `ReplaceArmWithCyberArm` in other tests removes arm then spawns and inserts – we can do the same but insert into cyber arm storage before the final `containerSystem.Insert(cyberArm, bodyComp.Organs!)`.

### 4. Edge Cases

- **Empty storage:** Do not push the modules line (keep examine concise)
- **Multiple cyber limbs:** Aggregate all stored items from all limbs into one list; avoid duplicate lines per limb
- **Stacked items:** Storage uses non-stacking, so each slot has one entity; show each distinct item (e.g. "Screwdriver, Cable" for two items)

## Files to Modify

| File | Change |
|------|--------|
| `Content.Shared/Cybernetics/Systems/CyberLimbInspectionSystem.cs` | Add BodySystem dependency; enumerate cyber limbs and storage; push modules markup |
| `Resources/Locale/en-US/cybernetics/cybernetics-maintenance.ftl` | Add `cyber-limb-inspection-modules` |
| `Content.IntegrationTests/Tests/Cybernetics/CyberLimbInspectionIntegrationTest.cs` | Add `WithDiagnosticGoggles_ExamineShowsInstalledModules` test |

## Verification

1. Run integration tests: `dotnet test Content.IntegrationTests --filter "FullyQualifiedName~CyberLimbInspection"`
2. In-game: spawn human, replace arm with cyber arm, insert item into limb storage (when detached or via panel-open flow), examine with diagnostic goggles – verify "Installed modules: Screwdriver" (or similar) appears

## Dependencies

- `BodySystem` (Content.Shared)
- `StorageComponent` (Content.Shared.Storage)
- `SharedStorageSystem` or direct `Container` access for reading (no insert needed in inspection)

## No SPDX Headers

Per plan requirements, do not add SPDX headers to any new or modified files in this implementation.
