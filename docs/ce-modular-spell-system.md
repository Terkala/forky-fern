# CE modular spell and mana action system

This document describes Funky Station’s **CE modular spell stack**: how mana actions compose YAML-driven effects, how they integrate with SS14’s action/DoAfter pipeline, which effect types exist, what spells use them today, and how to add new ones.

**Primary code paths**

| Area | Location |
|------|----------|
| Shared action system | `Content.Shared/_CE/Actions/CESharedActionSystem*.cs` |
| Modular events + runner | `Content.Shared/_CE/Actions/CESharedActionSystem.ModularEffects.cs` |
| Server-only hooks (speech, rune VFX) | `Content.Server/_CE/Actions/CEActionSystem.DoAfters.cs` |
| Spell effect base + args | `Content.Shared/_CE/Actions/Spells/CESpellEffect.cs` |
| Concrete effects | `Content.Shared/_CE/Actions/Spells/CESpell*.cs` |
| Action gating components | `Content.Shared/_CE/Actions/Components/CEAction*.cs` |
| Example spell YAML | `Resources/Prototypes/_CE/Entities/Actions/ElfMagic/*.yml`, `.../Elementalism/*.yml` |
| Skill grants | `Resources/Prototypes/_CE/Skill/Elf/elf_magic.yml` |
| Shared bases | `Resources/Prototypes/_CE/Entities/Actions/spell_base.yml` |

---

## Mental model

1. A **spell is an action entity**: it uses normal `Action` + `InstantAction` / `EntityTargetAction` / `WorldTargetAction` from SS14.
2. Instead of a one-off C# event type per spell, the action’s `event` is a **modular event** that holds **lists of `CESpellEffect` data definitions**.
3. **`CESharedActionSystem`** runs **telegraphy** effects when a cast DoAfter starts (predicted VFX-only style), then runs **main effects** when the action resolves.
4. **Mana cost** lives on **`CEActionManaCostComponent`** on the **action entity**, not inside individual `CESpellEffect` steps. Deduction happens on **`ActionPerformedEvent`** via **`MagicBatterySystem`** (container battery first, then user battery).

**Mage of Ascension** abilities (e.g. universal blink, create grimoire) use **custom action events**, not this modular stack. This document is about **`CE*ModularEffectEvent`** + **`CESpellEffect`**.

---

## Modular event types

Defined in `CESharedActionSystem.ModularEffects.cs`:

| Event type | SS14 action component | Typical use |
|------------|------------------------|-------------|
| `CEInstantModularEffectEvent` | `InstantAction` | Self-centered or no target cursor; `Target` in args is usually the performer. |
| `CEEntityTargetModularEffectEvent` | `EntityTargetAction` | Click entity; args get `Target` + target coordinates. |
| `CEWorldTargetModularEffectEvent` | `WorldTargetAction` | Click tile/world position; `Target` may be null, `Position` set. |

Each event has:

- **`telegraphyEffects`** — Run at **DoAfter start** (`CEActionStartDoAfterEvent`). Intended for cosmetic telegraphy only (plus shared client prediction). Server also uses this path for speech-linked telegraphy timing.
- **`effects`** — Run when the cast **completes** (main gameplay).

---

## Cast pipeline (order)

### 1. Validation and attempt

`CESharedActionSystem.Attempt.cs` subscribes **`ActionAttemptEvent`** / **`ActionValidateEvent`** on optional components:

| Component | Behavior |
|-----------|----------|
| `CEActionManaCostComponent` | Ensures user has `BatteryComponent`; shows low-mana warning if below cost (does not cancel on low mana at attempt—check code for exact behavior). Supports `CanModifyManacost` + `CECalculateManacostEvent`. |
| `CEActionFreeHandsRequiredComponent` | Requires free hands (`SharedHandsSystem.CountFreeHands`). |
| `CEActionSpeakingComponent` | Blocks if user is muted. |
| `CEActionStaminaCostComponent` | Blocks if stamina critical. |
| `CEActionDangerousComponent` | Blocks if pacified. |
| `CEActionSkillPointCostComponent` | Requires skill points on user’s skill storage. |
| `CEActionTargetMobStatusRequiredComponent` | Target must be a mob in allowed `MobState`s. |
| `CEActionSSDBlockComponent` | Blocks SSD targets. |

Server-only: **`CEActionRequiredMusicToolComponent`** (instrument playing while held).

### 2. DoAfter and telegraphy

If the action entity has **`DoAfterArgsComponent`**, `SharedActionsSystem` starts a DoAfter and raises **`CEActionStartDoAfterEvent`** on the performer’s **`TransformComponent`**.

Subscribers include:

- **Modular telegraphy**: runs **`telegraphyEffects`** from the modular event on the action (predicted).
- **`CEActionDoAfterSlowdownComponent`**: movement slowdown during cast.
- **Server** `CEActionSpeakingComponent` / `CEActionEmotingComponent`: IC speech/emote lines at start/end of DoAfter.
- **Server** `CEActionDoAfterVisualsComponent`: spawn attach child proto (rune) to performer, delete on DoAfter end.

### 3. Main effects and mana spend

When the action fires its event, **`OnInstantCast` / `OnEntityTargetCast` / `OnWorldTargetCast`** build **`CESpellEffectBaseArgs`** and iterate **`effects`**.

Then **`ActionPerformedEvent`** runs:

- **`CEActionManaCostComponent`**: drain from action **`ActionComponent.Container`** battery (if not innate), then user battery — **`MagicBatterySystem.ChangeMagicCharge`**.
- **`CEActionStaminaCostComponent`**, **`CEActionSkillPointCostComponent`**: apply costs.

---

## `CESpellEffectBaseArgs`

```csharp
// Conceptual shape (see CESpellEffect.cs)
public record CESpellEffectBaseArgs(
    EntityUid? User,      // Caster
    EntityUid? Used,      // Action container (often mind/action entity)
    EntityUid? Target,   // Resolved target; instant spells often set Target = User
    EntityCoordinates? Position
);
```

Each **`CESpellEffect.Effect(EntityManager, args)`** reads these to decide where to spawn entities, whom to stun, etc.

---

## Catalog of built-in `CESpellEffect` types

All under `Content.Shared/_CE/Actions/Spells/`. YAML uses **`!type:...`** matching the class name.

| YAML `!type` | Purpose | Notes |
|----------------|---------|--------|
| `CESpellSpawnEntityOnTarget` | `SpawnAtPosition` for each `spawns` proto at target tile/entity position | Often **server-only** (`IsClient` early return). |
| `CESpellSpawnEntityOnUser` | Spawn at **user** position | Server-only. |
| `CESpellSpawnInHandEntity` | Spawn at target, **`TryPickupAnyHand`** on **Target**; optional `DeleteIfCantPickup` | For instant casts, Target is typically caster. Server-only. |
| `CESpellSpawnEntitiesOnTargetInRadius` | Spawn center + 4 adjacent tiles | Server-only. |
| `CESpellApplyStatusEffect` | `StatusEffectsSystem` on **Target** | `statusEffect`, `duration`, `refresh`. |
| `CESpellApplyEntityEffect` | `SharedEntityEffectsSystem.ApplyEffects` on **Target** | `effects` list of `EntityEffect` (server-only field). |
| `CESpellApplyEntityEffectOnUser` | Same, on **User** | Server-only `effects`. |
| `CESpellStun` | `SharedStunSystem` knockdown + stun | `duration`, `dropItems`. |
| `CESpellStaminaDamage` | Stamina damage on target | See source for fields. |
| `CESpellProjectile` | Fire **`Prototype`** toward target/position via gun systems | Speed, spread, count, etc. |
| `CESpellArea` | Entities in **`Range`** of point; optional whitelist/blacklist; **`MaxTargets`**; nested **`Effects`** per entity | Composes other effects. |
| `CESpellThrowUserTo` / `CESpellThrowToUser` / `CESpellThrowFromUser` | Throw-style motion helpers | See source for parameters. |

To add behavior not covered here: create a **`partial class`** inheriting **`CESpellEffect`**, implement **`Effect`**, add **`[DataField]`** properties, and reference it from YAML with **`!type:YourEffect`**.

---

## Spell bases and VFX (YAML)

`Resources/Prototypes/_CE/Entities/Actions/spell_base.yml` defines:

- **`CEActionSpellBase`** — abstract action parent (rumble sound, big action style).
- **`CEBaseMagicRune`** — timed floor rune (sprite, light, despawn).
- **`CEBaseMagicImpact`** — short-lived impact animation.

Elf spells parent actions from **`CEActionSpellBase`** and reference rune/impact entity ids in **`CEActionDoAfterVisuals`** / **`CESpellSpawnEntityOnTarget`**.

---

## Spells implemented today (modular stack)

**Elf magic** and **Elementalism** use `CE*ModularEffectEvent` in YAML (see table).

| Action id | File | Targeting | Notable effects |
|-----------|------|-----------|-----------------|
| `CEActionSpellSphereOfLight` | `ElfMagic/sphere_of_light.yml` | Entity, range 5 | Spawn impact + apply **`CEStatusEffectGlowing`** 60s |
| `CEActionSpellCureWounds` | `ElfMagic/cure_wounds.yml` | Entity (mobs), DoAfter 1.5s | Telegraphy + heal-style entity effects (see YAML) |
| `CEActionSpellWaterCreation` | `ElfMagic/water_creation.yml` | Instant | Impact + spawn **`CELiquidDropWater`** in hand |
| `CEActionSpellElementalFireBolt` | `Elementalism/fire_bolt.yml` | World, range 20 | **`CESpellProjectile`** + fire bolt proto (**Heat** 10, ignite on collide, **`TimedDespawn`** cap) |
| `CEActionSpellElementalIceShards` | `Elementalism/ice_shards.yml` | World, range 18 | **`CESpellProjectile`** ×3 **`spread`** + shard proto **`SolutionInjectOnEmbed`** **`Fresium`** (`fun.yml`) |
| `CEActionSpellElementalDrainElectricity` | `Elementalism/drain_electricity.yml` | Entity, range 12 | **`CESpellApplyEntityEffect`** + **`Emp`** (tight **`maxRange`**, **50k** J, **60** s) |
| `CEActionSpellElementalElectricStrike` | `Elementalism/electric_strike.yml` | Entity, range 10 | **`CESpellApplyEntityEffect`** + **`Electrocute`** (**30** shock, **`bypassInsulation`**) |

**Elementalism level 5 (planned, see [ce-elementalism-mage-path.md](ce-elementalism-mage-path.md)):** **Fireball** — **`CESpellProjectile`** + upstream **`ProjectileFireball`** (wizard-parity explosive fireball). **Earthquake** — **`CESpellArea`** (large **`Range`**) + nested **`CESpellStun`** for wide knockdown.

**Electrocute + EMP on one target:** In a single **`CESpellApplyEntityEffect`**, list **`!type:Electrocute`** and **`!type:Emp`**; **`Emp`** pulses at the target’s position (see **Drain Electricity**). For EMP at a **world click** without an entity, use a **`CESpellEmpPulse`**-style effect or **`CESpellSpawnEntityOnTarget`** + **`TriggerOnSpawn`** / **`EmpOnTrigger`** marker (see mage path **Electric Strike** notes).

Skill tree **`ElfMagic`** (`Resources/Prototypes/_CE/Skill/trees.yml`) grants them via **`!type:AddAction`** in `Skill/Elf/elf_magic.yml`.

---

## Example: entity-target spell with DoAfter (Sphere of Light)

Full prototype lives in `Resources/Prototypes/_CE/Entities/Actions/ElfMagic/sphere_of_light.yml`. Condensed:

```yaml
- type: entity
  id: CEActionSpellSphereOfLight
  parent: CEActionSpellBase
  name: Sphere of Light
  components:
  - type: CEActionManaCost
    manaCost: 20
  - type: CEActionSpeaking
    startSpeech: "Appare in manu tua..."
    endSpeech: "sphaera lucis"
  - type: CEActionFreeHandsRequired
  - type: CEActionDoAfterVisuals
    proto: CERuneSphereOfLight
  - type: Action
    useDelay: 30
    icon:
      sprite: _CE/Actions/elf_magic.rsi
      state: sphere_of_light
  - type: DoAfterArgs
    delay: 0.5
  - type: TargetAction
    range: 5
  - type: EntityTargetAction
    whitelist:
      components:
      - MobState
      - Item
      - Anchorable
    event: !type:CEEntityTargetModularEffectEvent
      effects:
      - !type:CESpellSpawnEntityOnTarget
        spawns:
        - CEImpactEffectSphereOfLight
      - !type:CESpellApplyStatusEffect
        statusEffect: CEStatusEffectGlowing
        duration: 60
```

**Optional** `telegraphyEffects` on the same event would run at DoAfter start (VFX-only discipline).

---

## Example: instant spell with spawn + in-hand item (Water Creation)

From `Resources/Prototypes/_CE/Entities/Actions/ElfMagic/water_creation.yml` (condensed):

```yaml
- type: entity
  id: CEActionSpellWaterCreation
  parent: CEActionSpellBase
  name: Water creation
  components:
  - type: CEActionManaCost
    manaCost: 10
  - type: CEActionFreeHandsRequired
  - type: CEActionDoAfterVisuals
    proto: CERuneWaterCreation
  - type: Action
    useDelay: 10
  - type: DoAfterArgs
    delay: 1
  - type: InstantAction
    event: !type:CEInstantModularEffectEvent
      effects:
      - !type:CESpellSpawnEntityOnTarget
        spawns:
        - CEImpactEffectWaterCreation
      - !type:CESpellSpawnInHandEntity
        spawns:
        - CELiquidDropWater
```

For **`CEInstantModularEffectEvent`**, the system passes **`Target = performer`**, so **`CESpellSpawnInHandEntity`** picks up into the caster’s hands.

---

## Example: granting a spell via skill

```yaml
- type: skill
  id: MyNewSpellSkill
  skillUiPosition: 0, 0
  learnCost: 1
  tree: ElfMagic   # or another skillTree id you define
  icon:
    sprite: _CE/Actions/elf_magic.rsi
    state: water_creation
  effects:
  - !type:AddAction
    action: CEActionSpellWaterCreation
```

---

## Adding a new spell (checklist)

1. **Copy** the closest YAML from `Resources/Prototypes/_CE/Entities/Actions/ElfMagic/`.
2. Set a new **`id`**, **`name`**, **`description`**, **`Action.icon`**, **`CEActionManaCost.manaCost`**, **`useDelay`**, **`DoAfterArgs.delay`** as needed.
3. Choose **targeting**: `InstantAction` + `CEInstantModularEffectEvent`, or `EntityTargetAction` + `CEEntityTargetModularEffectEvent`, or `WorldTargetAction` + `CEWorldTargetModularEffectEvent`.
4. Fill **`effects`** (and optionally **`telegraphyEffects`**) with **`!type:CESpell...`** entries only, **or** add a new C# **`CESpellEffect`** subclass if you need new behavior.
5. Add any **rune/impact** entity protos (parent `CEBaseMagicRune` / `CEBaseMagicImpact` or custom).
6. **Grant** the action: skill **`AddAction`**, antag components, grimoire, admin spawn, etc.

---

## Mana and batteries

- Casters need **`BatteryComponent`** on the **mob** for mana checks and drain (typical CE / mage setups).
- If the action’s **`ActionComponent.Container`** is another entity with a battery (e.g. spell focus), that battery is drained first (`CESharedActionSystem.Performed.cs`).
- Overcharge/deficit behavior is controlled by **`CEEnergyOverchargeDamage`** / **`CEEnergyDeficitDamage`** and **`MagicBatterySystem`** events—not by the modular effect list.

---

## Related systems (not modular spells)

- **`Content.Shared/_CE/MagicEnergy/`** — `MagicBatterySystem`, `CESharedMagicEnergySystem`, alert/overcharge/deficit components.
- **Mage ascension** — `MageUniversalBlinkEvent`, grimoire, confluences, spell steal; separate from `CE*ModularEffectEvent`.

---

## Quick reference: file map

```
Content.Shared/_CE/Actions/
  CESharedActionSystem.cs              # Initialize, dependencies
  CESharedActionSystem.Attempt.cs      # Mana, hands, mute, skill points, …
  CESharedActionSystem.Performed.cs    # Mana/stamina/skill spend
  CESharedActionSystem.DoAfters.cs     # Slowdown from DoAfter
  CESharedActionSystem.ModularEffects.cs  # Telegraphy + effect execution
  CESharedActionSystem.Examine.cs
  Components/CEAction*.cs
  Events/CEActionsEvents.cs            # ICEMagicEffect cooldown (legacy-style)
  Spells/CESpellEffect.cs
  Spells/CESpell*.cs                   # One file per effect type

Content.Server/_CE/Actions/
  CEActionSystem.cs
  CEActionSystem.DoAfters.cs           # Speech, emote, rune attach/despawn

Resources/Prototypes/_CE/
  Entities/Actions/spell_base.yml
  Entities/Actions/ElfMagic/*.yml
  Entities/Actions/Elementalism/*.yml
  Skill/trees.yml
  Skill/Elf/elf_magic.yml
```

This file is maintained as documentation for contributors; game balance and exact field names should always be verified against the current YAML and C# sources.
