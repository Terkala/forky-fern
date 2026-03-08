# Immunosuppressant_ReducesBioRejectionDamage_WhenMetabolized — Failure Analysis Plan

## Test Overview

**Location:** `Content.IntegrationTests/Tests/Medical/ImmunosuppressantIntegrationTest.cs`

**Purpose:** Verifies that Immunosuppressant, when metabolized, reduces bio-rejection damage by granting an integrity immunity boost.

**Observed Failure:**
```
Assert.That(damageAfter, Is.LessThanOrEqualTo(damageBeforeImmunosuppressant))
Expected: less than or equal to 0.3
But was:  1
```

Bio-rejection damage increases from ~0.3 to 1.0 instead of decreasing after Immunosuppressant is added and metabolized.

---

## Test Flow Summary

1. Spawn `MobHuman` patient
2. Remove natural heart, insert `OrganBiosyntheticHeart` (integrity usage +1)
3. Apply `IntegrityPenaltyAppliedEvent` with capacity 6 (DirtyRoom penalty)
4. Run 70 ticks — expect bio-rejection damage to ramp up (≥ 0.1)
5. Add 10u Immunosuppressant to bloodstream via `TryAddToBloodstream`
6. Run 1800 ticks — expect damage to decrease or stay at 0
7. **Assert:** `damageAfter <= damageBeforeImmunosuppressant` — **FAILS**

---

## Root Cause Analysis

### 1. **Immunosuppressant Uses Non-Existent Metabolism Stage "Medicine"** (Primary)

**Evidence:**
- `Resources/Prototypes/Reagents/medicine.yml` (lines 302–308):
  ```yaml
  metabolisms:
    Medicine:
      metabolismRate: 0.5
      effects:
      - !type:AddIntegrityImmunityBoost
        amount: 1
        durationSeconds: 60
  ```
- `Resources/Prototypes/Chemistry/metabolism_stages.yml` defines only:
  - `Respiration`, `Digestion`, `Bloodstream`, `Metabolites`, `PlantMetabolisms`
  - **No `Medicine` stage exists**

**Impact:**
- `MetabolizerSystem.TryMetabolizeStage` iterates over organ stages (Respiration → Digestion → Bloodstream → Metabolites)
- When processing the Bloodstream stage, it checks `proto.Metabolisms.Metabolisms.TryGetValue(stage, out var entry)` with `stage = "Bloodstream"`
- Immunosuppressant has `metabolisms: Medicine`, so the lookup fails
- The reagent is treated as having no metabolism for Bloodstream → it is **transferred** to the Metabolites solution without applying effects
- `AddIntegrityImmunityBoost` is **never executed**

**Fix:** Change Immunosuppressant from `metabolisms: Medicine` to `metabolisms: Bloodstream` so it is metabolized when in the bloodstream.

---

### 2. **Wrong Target Entity for AddIntegrityImmunityBoost** (Secondary)

**Evidence:**
- `Content.Shared/EntityEffects/Effects/Body/AddIntegrityImmunityBoostEntityEffectSystem.cs`:
  - `EntityEffectSystem<OrganComponent, AddIntegrityImmunityBoost>` — requires target to have `OrganComponent`
  - Effect adds `IntegrityImmunityBoostComponent` to the **organ** that metabolizes
- `Content.Shared/Metabolism/MetabolizerSystem.cs` (lines 237–251):
  ```csharp
  default:
      _entityEffects.ApplyEffect(actualEntity, effect, scale);  // actualEntity = body
      break;
  ```
- `actualEntity = ent.Comp2?.Body ?? solutionOwner.Value` — the **body**, not the organ

**Impact:**
- Even if Immunosuppressant used `Bloodstream`, the effect would be raised on the body
- `AddIntegrityImmunityBoostEntityEffectSystem` subscribes to `EntityEffectEvent<AddIntegrityImmunityBoost>` on entities with `OrganComponent`
- The body has `BodyComponent`, not `OrganComponent` — the handler would not run
- `BioRejectionSystem` looks for `IntegrityImmunityBoostComponent` on **organs** via `_body.TryGetOrgansWithComponent<IntegrityImmunityBoostComponent>`

**Fix:** Add a special case in `MetabolizerSystem.ApplyEffect` for `AddIntegrityImmunityBoost` to pass the organ (`ent`) instead of the body (`actualEntity`).

---

### 3. **BioRejectionSystem Behavior (Reference)**

- `BioRejectionSystem` runs every 1 second
- Computes: `excess = usage + penalty - capacity`
- `capacity = baseCapacity + immunityBoost` (from organs with `IntegrityImmunityBoostComponent`)
- When `excess > 0`, damage ramps up; when `excess == 0`, damage ramps down
- Without immunity boost, excess stays positive → damage keeps increasing

---

## Verification Steps

1. **Confirm Medicine stage is missing:**
   - `grep -r "Medicine" Resources/Prototypes/Chemistry/` → no match in metabolism stages

2. **Confirm Immunosuppressant metabolism path:**
   - Reagent is in bloodstream → Bloodstream stage processes it
   - `TryGetValue("Bloodstream", ...)` on Immunosuppressant fails → transfer-only, no effects

3. **Confirm effect target mismatch:**
   - `AddIntegrityImmunityBoost` doc: "organ that metabolizes"
   - MetabolizerSystem passes body for default effects

---

## Recommended Fix Order

1. **Change Immunosuppressant to use Bloodstream** in `Resources/Prototypes/Reagents/medicine.yml`:
   ```yaml
   metabolisms:
     Bloodstream:
       metabolismRate: 0.5
       effects:
       - !type:AddIntegrityImmunityBoost
         amount: 1
         durationSeconds: 60
   ```

2. **Add special case for AddIntegrityImmunityBoost** in `Content.Shared/Metabolism/MetabolizerSystem.cs`:
   ```csharp
   case AddIntegrityImmunityBoost:
       _entityEffects.ApplyEffect(ent, effect, scale);
       break;
   ```

3. **Re-run test** to confirm it passes.

---

## Alternative: Add "Medicine" Metabolism Stage

If "Medicine" was intended as a distinct stage (e.g., processed by specific organs), one could:
- Add `Medicine` to `metabolism_stages.yml`
- Add a `Medicine` entry to `MetabolizerComponent.Solutions` for relevant organs (e.g., liver)
- Ensure the Medicine stage reads from the bloodstream (or appropriate solution)

This would require more changes and may not match the current metabolism pipeline design. The Bloodstream-based fix is simpler and aligns with how other injected medicines (e.g., Dermaline, Dexalin) work.
