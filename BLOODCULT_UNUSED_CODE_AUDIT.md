# BloodCult Unused Code Audit

This document lists all systems, events, components, and subscriptions in the BloodCult codebase that are implemented but never actually called/raised/used.

## 1. RuneDrawingEffectEvent System (CRITICAL - Fully Implemented but Never Called)

**Location:** 
- Event Definition: `Content.Shared/BloodCult/BloodCult.Abilities.cs` (lines 154-178)
- Client Subscription: `Content.Client/BloodCult/Systems/BloodCultRuneEffectSystem.cs` (line 22)
- Server: **NEVER RAISED** - No code raises this event

**Details:**
- The client system `BloodCultRuneEffectSystem` fully subscribes to `RuneDrawingEffectEvent` and has complete handlers for `Start` and `Stop` actions
- The event is designed to spawn overlay effect entities (`FxBloodCultRuneBarrier`, `FxBloodCultRuneEmpowering`, etc.) on the client
- These effect entities provide colored overlay animations during rune drawing
- The server should raise this event when a DoAfter starts, but it never does
- The `DrawRuneDoAfterEvent` has `EffectId` and `Duration` fields (lines 71-72) that are always set to `null` and `TimeSpan.Zero` (lines 257, 358, 364)

**Impact:** Visual overlay effects during rune drawing never appear. The drawing rune entities work (server-spawned), but the additional client-side overlay effects are missing.

**Fix Required:**
- Add method to map rune names to effect prototype names (e.g., `"BarrierRune"` → `"FxBloodCultRuneBarrier"`)
- Generate unique `EffectId` when starting DoAfter
- Call `RaiseNetworkEvent(new RuneDrawingEffectEvent(...))` with `Action.Start` when DoAfter begins
- Call `RaiseNetworkEvent(new RuneDrawingEffectEvent(...))` with `Action.Stop` when DoAfter completes/cancels

---

## 2. CultHealingSystemUpdatedEvent (Commented Out, Never Defined)

**Location:** `Content.Server/BloodCult/EntitySystems/CultHealingSourceSystem.cs` (line 212)

**Details:**
- Line 212 has: `//RaiseLocalEvent(new CultHealingSystemUpdatedEvent());`
- This event is never defined anywhere in the codebase
- No system subscribes to this event
- Appears to be leftover from planned functionality

**Impact:** None - code is commented out, so it doesn't break anything.

---

## 3. CultMarkedComponent (Component Exists, Handler Commented Out)

**Location:**
- Component: `Content.Server/BloodCult/Components/CultMarkedComponent.cs`
- Subscription: `Content.Server/BloodCult/EntitySystems/CultistSpellSystem.cs` (line 103) - **COMMENTED OUT**
- Usage: Line 477 has commented code `//EnsureComp<CultMarkedComponent>(target);`
- Handler: Lines 494-504 have commented out `OnMarkedAttacked` method

**Details:**
- Component is defined and registered
- Subscription to `AttackedEvent` is commented out
- Handler method exists but is commented out
- Component is never added to entities (commented out on line 477)

**Impact:** The "cult marking" feature appears to be disabled/incomplete. If this was meant to track marked targets for some mechanic, it's not functional.

---

## 4. TwistedConstructionDoAfterEvent (Commented Out - Planned Feature)

**Location:** `Content.Server/BloodCult/EntitySystems/CultistSpellSystem.cs`

**Details:**
- Line 108: `//SubscribeLocalEvent<BloodCultistComponent, TwistedConstructionDoAfterEvent>(OnTwistedConstructionDoAfter);`
- Lines 629-684: Entire wall/girder deconstruction feature is commented out (includes DoAfter creation)
- Lines 687+: Handler method `OnTwistedConstructionDoAfter` is commented out
- The main Twisted Construction spell (plasteel conversion) works fine as an instant spell (lines 510-626)
- The DoAfter code appears to be for a planned feature (wall/girder deconstruction) that was never implemented or was disabled

**Impact:** None - the main Twisted Construction spell works. This is dead code for an unimplemented feature (wall/girder deconstruction via DoAfter).

**Note:** The event is defined in `BloodCult.Abilities.cs` (lines 96-101) but is only referenced in commented-out code.

---

## 5. DrawRuneDoAfterEvent.EffectId and Duration Fields (Never Used)

**Location:** `Content.Shared/BloodCult/BloodCult.Abilities.cs` (lines 71-72)

**Details:**
- `DrawRuneDoAfterEvent` has `EffectId` and `Duration` fields
- These are always set to `null` and `TimeSpan.Zero` when creating DoAfter events:
  - Line 257 in `BloodCultRuneCarverSystem.cs`: `null, TimeSpan.Zero`
  - Line 358 in `BloodCultRuneCarverSystem.cs`: `null, TimeSpan.Zero`
  - Line 364 in `BloodCultRuneCarverSystem.cs`: `null, TimeSpan.Zero`
- These fields appear to be intended for use with `RuneDrawingEffectEvent` but are never populated

**Impact:** Part of the broken `RuneDrawingEffectEvent` system. These fields should store the effect ID and duration for the overlay effects.

---

## 6. HereticRitualRuneComponent Subscription (Commented Out)

**Location:** `Content.Server/BloodCult/EntitySystems/BloodCultRuneCarverSystem.cs` (line 67)

**Details:**
- Line 67: `//SubscribeLocalEvent<HereticRitualRuneComponent, InteractHandEvent>(OnInteract);`
- `HereticRitualRuneComponent` doesn't appear to exist in the BloodCult codebase
- This looks like leftover code from a different system (possibly Heretic/old cult system)

**Impact:** None - commented out and component doesn't exist.

---

## Summary

### Critical Issues (Functionality Broken):
1. **RuneDrawingEffectEvent** - Complete system implemented but never called

### Minor Issues (Incomplete/Unused):
3. **CultMarkedComponent** - Feature appears disabled/incomplete
4. **DrawRuneDoAfterEvent.EffectId/Duration** - Fields exist but never used (related to #1)
5. **TwistedConstructionDoAfterEvent** - Planned feature (wall/girder deconstruction) that was never implemented

### Cleanup Needed (Dead Code):
5. **CultHealingSystemUpdatedEvent** - Referenced but never defined
6. **HereticRitualRuneComponent** - Commented out reference to non-existent component

---

## Recommendations

1. **Fix RuneDrawingEffectEvent** - This is a complete visual effect system that just needs to be wired up
2. **Decide on CultMarkedComponent** - Either implement it or remove the component
3. **Clean up commented code** - Remove commented-out subscriptions, undefined event references, and dead DoAfter code for unimplemented features
