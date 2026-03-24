# Elementalism (Mage of Ascension) — spell path plan

Design references: [Mage of Ascension — Elementalism](https://github.com/Terkala/docs/blob/mage-of-ascension/src/design-proposals/mage-of-ascension.md), [CE modular spell system](ce-modular-spell-system.md).

**Legend — implementation**

| Tag | Meaning |
|-----|---------|
| **CE** | Cast + immediate effects can use `CE*ModularEffectEvent` + existing `CESpellEffect` types (YAML-first). |
| **Custom** | Needs dedicated C# (`EntitySystem`, custom `InstantAction`/`EntityTargetAction` event, or new `CESpellEffect` subclass). |
| **CE + custom** | Cast pipeline is modular; specific behavior needs a small custom component/system (often on a spawned entity). |

---

## Spell chart

| Level / gate | Spell | Role | Summary | CE / custom |
|--------------|-------|------|---------|-------------|
| **1 — automatic** | **Summon Rock** | Offense | Summons a rock; **15 damage** and **knockdown** when thrown (behavior on the rock entity / throw collision). | **CE** (modular cast: spawn in hand; combat on **item prototype** — damage, stun/knockdown on impact). **Custom** only if no existing item/thrown pattern fits and you need a one-off spell effect. |
| **1 — automatic** | **Ice Shield** | Defense | Summons an **ice shield** into a hand using whatever hand-pickup behavior the spell system already uses (no special slot). **Melts after a fixed lifetime from spawn** (**variant A**): prototype **`TimedDespawn`** (`lifetime` **60** s = 1 minute, tunable). Timer runs **while held or in inventory** — shield disappears when time expires. Optional **`SpawnOnDespawn`** for melt puddle/VFX. | **CE**: **`CEInstantModularEffectEvent`** + **`CESpellSpawnInHandEntity`**; shield entity YAML only. |
| **2 — choice** | **Fire Bolt** | Offense | **Implemented (Tier 2 / CE):** **`CEActionSpellElementalFireBolt`** — `CEWorldTargetModularEffectEvent` + **`CESpellProjectile`** (`Resources/Prototypes/_CE/Entities/Actions/Elementalism/fire_bolt.yml`). **`Heat: 10`**, **`IgniteOnCollide`** + **`IgnitionSource`**, **`TargetAction.range` 20**, bolt **`TimedDespawn` 1 s** × **`projectileSpeed` 20** ≈ **20 tile** flight cap. **Stops on first hard collision** (typical `BaseBullet`); **no** pierce-all-mobs, **no** full trail-of-tiles ignition. | **CE** (Tier 2). Original “full spec” (pierce, trail fire, anchored-only) remains **custom** if revisited. |
| **2 — choice** | **Earthen Barricade** | Defense | Spawns a **stalagmite** at the targeted **world/tile** location (anywhere valid). Visual/gameplay base: cave/dungeon **`FloraStalagmite`** (`Resources/Prototypes/Entities/Objects/Decoration/flora.yml`, parents `BaseRock`). | **CE** if `CEWorldTargetModularEffectEvent` + `CESpellSpawnEntityOnTarget` (or equivalent) can place the proto with correct snap/collision; **CE + custom** if you need placement rules (clearance, map bounds, anti-cheese) beyond what the shared effect provides. May use a **child prototype** of `FloraStalagmite` for mage-spawn metadata only. |
| **3 — choice** | **Ice Shards** | Offense | **Implemented (CE):** **`CEActionSpellElementalIceShards`** (`Elementalism/ice_shards.yml`). **`CEWorldTargetModularEffectEvent`** + **`CESpellProjectile`** — **`projectileCount` 3**, **`spread` 0.35**, **`projectileSpeed` 18**, **`TimedDespawn` 1** s. **`CEProjectileElementalIceShard`**: **`EmbeddableProjectile`**, **`SolutionInjectOnEmbed`** (**`pierceArmor: true`**) + **2 u** **`Fresium`** (`fun.yml`; localized name **Freezium** via **`reagent-name-fresium`**). Hit damage **Cold 4** / **Piercing 6**; **`deleteOnRemove: true`** on embed. **Mana 22**, **`useDelay` 8** s, range **18**, DoAfter **0.4** s. | **CE**. Targets **without** a bloodstream get impact damage but **no** bloodstream chill. **Custom** only for per-embed **`ChangeHeat`** ticks instead of chem. |
| **3 — choice** | **Drain Electricity** | Defense | **Implemented:** **`CEActionSpellElementalDrainElectricity`** (`Elementalism/drain_electricity.yml`). **`EmpPulse`** at target with **small radius** (**`maxRange` 1.25** m, `rangeModifier` 2 → effective **1.25**) but **grenade-grade “intensity”**: **`energyConsumption` 50000** J, **`duration` 60** s disable (same as **`EmpGrenade`** / `EmpOnTrigger` defaults). Hits the target and **very** nearby entities on the same tile/weld. | **CE** — **`CEEntityTargetModularEffectEvent`** + **`CESpellApplyEntityEffect`** + **`!type:Emp`**. **Custom** only for **`TryEmpEffects` only** (zero-radius) if you ever drop `EmpPulse`. |
| **4 — choice** | **Electric Strike** | Offense | **Implemented (CE):** **`CEActionSpellElementalElectricStrike`** (`Elementalism/electric_strike.yml`). **`CEEntityTargetModularEffectEvent`** + **`CESpellApplyEntityEffect`** + **`!type:Electrocute`** — **`shockDamage` 30**, **`electrocuteTime` 3**, **`bypassInsulation: true`**. **`useDelay` 10** s, **`TargetAction.range` 10**, **mana 35**, DoAfter **0.45** s. Impact + rune VFX only (no caster→target beam). | **CE** for combat; **CE + custom** only for **line/lightning VFX** or special shock rules. |
| **4 — choice** | **Fire Barrier** | Defense | *(Moved from level 3.)* **Toggle**: while **on**, a **constant fire aura** surrounds the mage, **drains 0.5 mana / second**, mage is **immune to fire and heat damage**, and **cannot gain fire stacks** (`FlammableComponent`). **Contact** (bumping / overlapping the barrier) applies **~½ of the target’s max fire stacks** (e.g. default human `MaximumFireStacks` 10 → **+5**), clamped. | **Custom** (toggle + per-tick mana, damage immunity / stack suppression, contact sensor + stack grant). See **Fire Barrier** under Level 4. |
| **5 — choice** | **Fireball** | Offense | **Wizard-parity explosive shot**, built on **CE** modular cast: **`CEWorldTargetModularEffectEvent`** + **`CESpellProjectile`** with prototype **`ProjectileFireball`** (or a **`_CE` child** that parents it for mage-only metadata). Matches wizard **`ActionFireball`**: world aim, **`BulletRocket`‑style** flight, **Heat 10** on direct hit, **`Explosive`** (**Default**, **`totalIntensity` 200**, **`maxTileBreak` 0**, etc. — see `Resources/Prototypes/Entities/Objects/Weapons/Guns/Projectiles/magic.yml`), **`IgniteOnCollide`**, **`IgnitionSource`**. Tune **`projectileSpeed`** / **`TimedDespawn`** on the proto if CE shot speed must align with **`ProjectileSpellEvent`** behavior. Mana / DoAfter / `useDelay` / `TargetAction.range` are mage YAML (wizard baseline: **`useDelay` 15** s, **range 60**). | **CE** (same as Fire Bolt’s pipeline, heavier projectile entity reused from upstream). |
| **5 — choice** | **Earthquake** | Defense | **Quake-style knockdown** in a **large radius**: cast picks a **world center** (or self-centered pattern if you use instant + offset — default assumption: **world target** like other ground AoEs). **`CESpellArea`** with **large `Range`**, nested **`CESpellStun`** ( **`TryKnockdown` + `TryAddStunDuration`** per target — see `CESpellStun.cs`). Optional **`Whitelist`** (e.g. mobs only), **`MaxTargets`**, **`AffectCaster: false`** so the mage is not flattened unless intentional. No tile damage — control spell, not **`Explosive`**. | **CE** — pure composition of stock area + stun effects. |
| *TBD* | **Capstone** (final confluence) | Ascension | Persistent ice field / terrain / freeze (per Mage of Ascension doc). | **Custom** (maintained effect, tile/wall replacement, balance). |

---

## Why these spells are not “pure CE modular” spells

**Definition:** A spell uses the **CE base** in the sense of [ce-modular-spell-system.md](ce-modular-spell-system.md) when the cast is handled by **`CEInstantModularEffectEvent` / `CEEntityTargetModularEffectEvent` / `CEWorldTargetModularEffectEvent`** and **every gameplay step** is implemented by **existing** `CESpellEffect` `!type:` entries (spawn, status, stun, projectile, area, entity effects, etc.) with **no new C#** effect types and **no ongoing `EntitySystem`** tied to the spell.

Anything that needs a **new `CESpellEffect` subclass**, **custom action event**, or **server tick / event subscription** for core behavior is **outside** that base (though the **cast** can still be modular if you add a thin custom effect).

### Spells that *can* use the CE base (as designed)

| Spell | Why it fits |
|-------|-------------|
| **Summon Rock** | One-shot **`CESpellSpawnInHandEntity`** (or spawn + pickup). **15 damage + knockdown** live on the **rock prototype** (thrown/projectile/item components), not in the spell effect list. |
| **Ice Shield** | **`CESpellSpawnInHandEntity`** + shield prototype with **`TimedDespawn`** (and optional **`SpawnOnDespawn`**). No custom melt system. |
| **Earthen Barricade** | **`CEWorldTargetModularEffectEvent`** + **`CESpellSpawnEntityOnTarget`** (or equivalent) to place **`FloraStalagmite`**. Extra placement validation is optional **CE + custom** if you add rules beyond the stock spawn effect. |
| **Drain Electricity** | **`CEActionSpellElementalDrainElectricity`**: **`Emp`** effect with **`maxRange` / `rangeModifier`** for **tight** pulse; **`energyConsumption`** + **`duration`** match **`EmpGrenade`** (high drain + long disable). `EmpPulse` API: `range`, `energyConsumption`, and `duration` are **independent** (`SharedEmpSystem.EmpPulse`). |
| **Fire Bolt** | **Tier 2 (implemented):** **`CEWorldTargetModularEffectEvent`** + **`CESpellProjectile`** + **`CEProjectileElementalFireBolt`** (`fire_bolt.yml`). Aspirational behaviors (pierce all mobs, ignite every tile along path, anchored-only stop) are **not** this build — those would be **custom**. |
| **Fireball (level 5)** | Same pattern as Fire Bolt: **`CESpellProjectile`** fires **`ProjectileFireball`**; explosion and ignition are **on the projectile entity**, not new spell effect types. |
| **Earthquake (level 5)** | **`CESpellArea`** runs **`CESpellStun`** on each entity in range — no per-tick system required. |
| **Electric Strike (level 4)** | **`CESpellApplyEntityEffect`** with stock **`Electrocute`** entity effect (`bypassInsulation` in YAML). Stun/shock pipeline is upstream, not a new `CESpellEffect` type. |
| **Ice Shards (level 3)** | **`CEActionSpellElementalIceShards`**: **`CESpellProjectile`** ×3 + **`CEProjectileElementalIceShard`** with **`SolutionInjectOnEmbed`** + **`Fresium`** (`fun.yml`). |

### Spells that cannot use the CE base alone — reasons

| Spell | Why stock `CESpellEffect` + modular event is not enough |
|-------|-----------------------------------------------------------|
| **Ice Shards** | **Only** if you replace **bloodstream `Fresium`** inject with **direct `ChangeHeat` every tick while embedded** — that needs a **custom** shard component/system. The **implemented** spell uses **upstream `Fresium`** metabolism (`fun.yml`). |
| **Drain Electricity** | **Only** if you require **`TryEmpEffects` on exactly one entity** with **no** `EmpPulse` radius. The stock **`Emp`** **entity effect** always calls **`EmpPulse`** (AoE at coordinates), which **is** composable via **`CESpellApplyEntityEffect`** — so “pure CE” is viable for **grenade-like** EMP on the target; see table above. |
| **Electric Strike** | **Only** if you insist on **caster→target beam VFX** drawn every cast — that is **custom** art/system. The **shock + stun + bypass gloves** path is **`!type:Electrocute`** via **`CESpellApplyEntityEffect`** (see **`electric_strike.yml`**). |
| **Fire Barrier** | **Toggle** with **persistent on-state**, **0.5 mana/sec** drain while active, **damage resist / fire-stack suppression on self**, and **physics contact** to grant **fire stacks to others** are all **ongoing systems** and **collision subscriptions**. The modular pipeline is **one-shot on cast**; it does not model **sustained auras**, **per-tick mana**, or **fixture-based touch damage**. |
| **Capstone** | **Maintained** world rewrite (**tiles/walls → ice**), **large AoE freeze**, and ascension-scale balance need **custom world/tile interaction** (and likely prediction/PVS considerations). None of the stock spell effects modify **map geometry** or run **continuous terrain replacement**. |

---

## Level 1 — design notes

### Summon Rock

- **Player-facing**: single action; creates a throwable rock (one free hand or explicit hand rules TBD to match other mage actions).
- **Implementation sketch**: action with `CEInstantModularEffectEvent` / `CEEntityTargetModularEffectEvent` as appropriate; `CESpellSpawnInHandEntity` (or equivalent) spawns prototype e.g. `CEElementalSummonRock` with:
  - Thrown / projectile or melee-throw damage tuned to **15** on hit.
  - Knockdown via `CESpellStun`-like outcome **on hit** — usually **on the rock** (`DamageOtherOnHit` + stun component, or shared throw systems), not in the spell’s effect list, so the spell YAML stays modular.

### Ice Shield (**locked: pure CE, variant A**)

- **Player-facing**: shield goes to **whatever hand** the shared spawn effect chooses. It **vanishes after a fixed time from cast** (**default `TimedDespawn.lifetime: 60`** seconds). The clock runs **while the shield is held, in a bag, or on the ground** — it does **not** pause in inventory.
- **Implementation**:
  - **`CEInstantModularEffectEvent`** + **`CESpellSpawnInHandEntity`** → spawn shield prototype.
  - Prototype: **`TimedDespawn`** (`Robust.Shared.Spawners`; see `CEBaseMagicRune` / `CEBaseMagicImpact` in `_CE/Entities/Actions/spell_base.yml` for examples). Optional **`SpawnOnDespawn`** for melt puddle or effect on delete.
- **Dropped-triggered melt** is **out of scope** for this path (would require custom code again).

---

## Level 2 — design notes

### Fire Bolt (**Tier 2 — implemented**)

- **Prototypes**: `CEActionSpellElementalFireBolt`, `CEProjectileElementalFireBolt`, `CERuneElementalFireBolt` in `_CE/Entities/Actions/Elementalism/fire_bolt.yml`.
- **Cast**: `CEWorldTargetModularEffectEvent` → **`CESpellProjectile`** (`prototype: CEProjectileElementalFireBolt`, **`projectileSpeed: 20`**). DoAfter **0.35 s**, **`useDelay` 4 s**, **mana 12**, **`TargetAction.range` 20**.
- **Projectile** (parents **`BaseBullet`**): **`Heat: 10`**, **`IgniteOnCollide`** (0.35 stacks, same order of magnitude as **`ProjectileFireball`**), **`IgnitionSource`**, orange **`BulletImpactEffectOrangeDisabler`**, **`TimedDespawn`** **`lifetime: 1`** (keep in sync with speed: max travel ≈ **speed × lifetime** tiles).
- **Not in this tier**: pierce-all-mobs, ignition of **every** tile along the segment, bolt as **`FlammableComponent`**, stop **only** on anchored statics — those need a **custom** projectile or sweep if restored later.

### Earthen Barricade

- **Prototype**: start from **`FloraStalagmite`** — already used in procedural dungeon/cave content (`dungeon_configs`, biome templates). It parents **`BaseRock`** (destructible spike, collision). If the barricade should differ (HP, no random sprite roll, mage-owned despawn), add **`CEElementalEarthenBarricade`** parented from `FloraStalagmite`.
- **Placement**: world click → grid-aligned spawn; validate not inside solid full tile if required (custom or shared helper).

---

## Level 3 — design notes

### Ice Shards (**implemented — CE + upstream Freezium / `Fresium`**)

- **Action / VFX**: **`CEActionSpellElementalIceShards`**, **`CERuneElementalIceShards`** in **`Resources/Prototypes/_CE/Entities/Actions/Elementalism/ice_shards.yml`**.
- **Reagent**: Shards prefill **`ReagentId: Fresium`** (prototype **`Fresium`**, **`Resources/Prototypes/Reagents/fun.yml`**). That reagent is the stock “freeze juice” / **Freezium**-style cold chem (`AdjustTemperature`, movement effects at higher dose, etc.). **No `_CE` reagent fork** — balance lives entirely in upstream **`fun.yml`** and locale (**`Resources/Locale/en-US/reagents/meta/fun.ftl`**).
- **Shard**: **`CEProjectileElementalIceShard`** — parents **`BaseBullet`**; **`deleteOnCollide: false`** so embed is not deleted before inject; **`EmbeddableProjectile`** (**`embedOnThrow: false`**, **`deleteOnRemove: true`**); **`SolutionContainerManager`** solution **`shard`** with **2 u** **`Fresium`**; **`SolutionInjectOnEmbed`** **`transferAmount` 2**, **`pierceArmor: true`**.
- **Pipeline**: **`EmbedEvent`** → **`SolutionInjectOnCollideSystem`** (`Content.Server/Chemistry/EntitySystems/SolutionInjectOnEventSystem.cs`) → bloodstream (see **`arrows.yml`** / **`BaseArrow`**).

**Alternate (not implemented):** per-embed **`ChangeHeat`** ticks via a custom shard component if you must chill entities **without** bloodstreams.

### Drain Electricity (**implemented**)

- **Prototypes**: `CEActionSpellElementalDrainElectricity`, rune/impact FX, in **`_CE/Entities/Actions/Elementalism/drain_electricity.yml`**.
- **Cast**: **`CEEntityTargetModularEffectEvent`**; **`TargetAction.range` 12**; DoAfter **0.4 s**; **mana 25**; **`useDelay` 12 s**; server impact spawn + **`CESpellApplyEntityEffect`** → **`!type:Emp`** with **`scaling: false`**.
- **EMP tuning**: **`maxRange: 1.25`**, **`rangeModifier: 2`** → pulse radius **1.25** m (see `EmpEntityEffectSystem`: `min(rangeModifier * scale, maxRange)`; spell **scale** is **1**). **`energyConsumption: 50000`**, **`duration: 60`** — align with **`EmpGrenade`** punch while avoiding **5.5** m room-wide EMP.
- **Strict single-target** (no `EmpPulse` at all): still **`custom`** (`TryEmpEffects` only).
- **VFX/SFX**: `EmpPulse` spawns **`EffectEmpPulse`** and plays EMP audio (`SharedEmpSystem`).

---

## Level 4 — design notes

### Electric Strike (**Option 1 — implemented as CE**)

- **Prototype**: **`CEActionSpellElementalElectricStrike`** in **`Resources/Prototypes/_CE/Entities/Actions/Elementalism/electric_strike.yml`**.
- **Mechanism**: **`Electrocute`** entity effect (`Content.Shared/EntityEffects/Effects/StatusEffects/ElectrocuteEntityEffectSystem.cs`) → **`TryDoElectrocution`** with **`ignoreInsulation`** from YAML **`bypassInsulation`**.
- **Tuning**: **`shockDamage`**, **`electrocuteTime`**, **`siemensCoefficient`**, **`refresh`** on **`!type:Electrocute`**; **`scaling: false`** for fixed damage at spell scale **1**.
- **VFX**: **`CEActionDoAfterVisuals`** rune + **`CESpellSpawnEntityOnTarget`** impact — **no** automatic line from caster to victim (**custom** if required).

#### Combining **Electrocute** (Option 1) with **EMP** — all stay CE

Use **one** **`CESpellApplyEntityEffect`** with **multiple** entity effects (order is apply order; both run on the **same spell target**):

1. **Electrocute + Emp (target-centered pulse)** — Add **`!type:Emp`** after **`!type:Electrocute`**. The pulse is centered on the **victim’s transform** (same as **Drain Electricity**). Tune **`maxRange` / `rangeModifier`**, **`energyConsumption`**, **`duration`** for a **wider** “arc fries their gear” feel or a **tight** personal bubble (copy **Drain**’s **1.25** m for parity).
2. **EMP-only on valid Transform targets** — If the whitelist allows **machines** (`Anchorable` / `Item`), **`Electrocute`** may no-op or partially apply where there is no **`StatusEffectsComponent`**, while **`Emp`** still runs for **`TransformComponent`** — so one action can **tag APCs/borgs** with EMP and **mobs** with shock when the same proto is used on mixed targets; tighten **`EntityTargetAction.whitelist`** if you want **mobs-only** shock+EMP.
3. **Separate actions** — Keep **Electric Strike** pure shock and **Drain Electricity** pure EMP for clarity and balance; no YAML duplication on one button.
4. **Self-centered EMP + entity-target shock** — Not a single target: you’d cast **Electrocute** on a **mob** and use **`CESpellApplyEntityEffectOnUser`** + **`Emp`** in an **`CEInstantModularEffectEvent`** spell for a **different** fantasy (“you surge and pulse the room”). That is a **second spell**, not a bolt at someone.

**No new `CESpellEmpPulse`** is required to combine Option 1 with EMP **on the same entity** — reuse **`!type:Emp`** inside **`CESpellApplyEntityEffect`**. World-position-only EMP still wants **`CESpellEmpPulse`** or the **TriggerOnSpawn** marker pattern (see [ce-modular-spell-system.md](ce-modular-spell-system.md) discussion).

### Fire Barrier

- **Toggle**: same action toggles on/off, or paired enable/disable — predict carefully if any client UI shows state (`[NetworkedComponent]` on mage or action).
- **Mana**: while active, server **`Update`** (or timer): **`MagicBatterySystem.ChangeMagicCharge`** (or mage battery API) **−0.5 × Δt** per second equivalent; turn off at **0** mana if desired.
- **Fire visuals**: point light + sprite overlay / particles on mage; optional **small flammable fixture** only for **others** (not for self-ignition).
- **Self immunity**:
  - **Heat / burn damage**: `DamageSpecifier` resistance, relevant `Damageable` modifier, or subscription that zeroes fire-type damage to self while component active.
  - **Fire stacks**: component on mage that **blocks positive** `FireStacks` adjustments (see `FireEvents` / server flammable system) or sets stacks to 0 each tick — prefer an **event** hook so you do not fight other systems every frame.
- **Contact ignition**: **secondary fixture** (slightly larger than mob hitbox) **sensor** colliding with other **mobs** → apply **`+ (their Flammable.MaximumFireStacks × 0.5)`** (clamp to max), with **icooldown** per target so hugging does not melt them in one tick. Tune overlap vs grabs/disarms.

---

## Level 5 — design notes

### Fireball (**locked: CE, wizard-parity**)

- **Reference**: wizard **`ActionFireball`** (`Resources/Prototypes/Magic/projectile_spells.yml`) → **`ProjectileSpellEvent`** + **`ProjectileFireball`**. Stats live on **`ProjectileFireball`** in `magic.yml` (Heat, Explosive, ignite, light).
- **CE cast**: **`CEWorldTargetModularEffectEvent`** → **`CESpellProjectile`** (`prototype: ProjectileFireball`, tune **`projectileSpeed`**, optional **`Spread`** / **`ProjectileCount`** left at **1** for parity).
- **Flight cap**: If the wizard ball relies on engine default lifetime, add or match **`TimedDespawn`** on a **`_CE` fork** of the projectile so max range stays predictable (same lesson as Fire Bolt).

### Earthquake (**locked: CE**)

- **Cast**: **`CEWorldTargetModularEffectEvent`** (recommended: epicenter at cursor) or **`CEInstantModularEffectEvent`** with area centered on caster — document which in YAML.
- **Effects**: single **`CESpellArea`** with high **`Range`**; **`Effects`** list contains **`!type:CESpellStun`** with **`Duration`** tuned for knockdown feel (and **`DropItems`** if desired).
- **Filtering**: use **`Whitelist`** / **`Blacklist`** / **`MaxTargets`** on **`CESpellArea`** to avoid stunning ghosts, machines, or the whole station; **`AffectCaster`** explicit default.

---

## Changelog

- **2026-03-21** — Chart created; Level 1 automatic spells: Summon Rock, Ice Shield.
- **2026-03-21** — Level 2 choice: Fire Bolt, Earthen Barricade; stalagmite proto documented as `FloraStalagmite`.
- **2026-03-21** — Ice Shield: any-hand spawn; melt only on **drop** to world (simplified spec).
- **2026-03-21** — Level 3 choice: Ice Shards, Fire Barrier.
- **2026-03-21** — Ice Shards: ~~per-shard `ChangeHeat` ticks~~ superseded **2026-03-24** by **`Fresium`** inject + **`SolutionInjectOnEmbed`** (CE).
- **2026-03-21** — Level 3 defense → **Drain Electricity** (targeted EMP); **Fire Barrier** moved to **level 4**. Level 4 offense: **Electric Strike**.
- **2026-03-21** — Added **“Why not pure CE modular”** section (per-spell reasons vs stock `CESpellEffect` catalog). **Drain Electricity** clarified: **`Emp` entity effect** enables **CE** for `EmpPulse`-style EMP; **custom** only for **`TryEmpEffects`-only**.
- **2026-03-21** — **Ice Shield**: documented **pure CE** options (`TimedDespawn`, `SpawnOnDespawn`) vs **drop-triggered** melt.
- **2026-03-21** — **Ice Shield** locked to **pure CE variant A** (`TimedDespawn`, **60** s default). Removed drop-based melt from spec.
- **2026-03-21** — **Fire Bolt** Tier 2 implemented: `fire_bolt.yml`, **Heat 10**, CE modular projectile.
- **2026-03-23** — **Drain Electricity** implemented: `drain_electricity.yml` — tight **`EmpPulse`** (**1.25** m), **50k** J / **60** s intensity (grenade-parity).
- **2026-03-24** — **Level 5** locked: **Fireball** (CE **`CESpellProjectile`** + **`ProjectileFireball`**, wizard-parity); **Earthquake** (**`CESpellArea`** + **`CESpellStun`**, large-radius knockdown).
- **2026-03-24** — **Electric Strike**: CE **`Electrocute`** entity effect (`electric_strike.yml`); doc’d **EMP + Electrocute** combos (same `CESpellApplyEntityEffect` list).
- **2026-03-24** — **Ice Shards** implemented: **`ice_shards.yml`**, **`CEIceShardsSpellTest`**; **`SolutionInjectOnEmbed`** + upstream **`Fresium`** (`fun.yml`).
- **2026-03-24** — **Ice Shards**: dropped **`CEFrozium`** / **`_CE/reagents/elementalism.yml`** / **`_CE/reagents/frozium.ftl`**; shards use stock **`Fresium`** (Freezium) only.
