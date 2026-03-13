using System.Linq;
using Content.Server.Atmos.Components;
using Content.Server.Atmos.EntitySystems;
using Content.Server.Atmos.Piping.Components;
using Content.Server.Explosion.EntitySystems;
using Content.Server.Power.Generation.Supermatter.Components;
using Content.Server.Power.Generation.Supermatter.Events;
using Content.Shared.Atmos;
using Content.Shared.Damage;
using Content.Shared.Damage.Systems;
using Content.Shared.FixedPoint;
using Content.Shared.Item;
using Content.Shared.Mobs.Components;
using Content.Shared.Power.Generation.Supermatter.Components;
using Content.Shared.Power.Generation.Supermatter.EntitySystems;
using Content.Shared.Power.Generation.Supermatter;
using Content.Shared.Audio;
using Content.Shared.Singularity.Components;
using Content.Shared.Singularity.EntitySystems;
using Robust.Server.GameObjects;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Audio;
using Robust.Shared.Containers;
using Robust.Shared.Map;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Systems;

namespace Content.Server.Power.Generation.Supermatter;

/// <summary>
/// Server system for Supermatter processing. Runs on atmos ticks via AtmosDeviceUpdateEvent.
/// Reads/writes SupermatterProcessingComponent; updates SupermatterStateComponent for client.
/// </summary>
public sealed partial class SupermatterSystem : SharedSupermatterSystem
{
    [Dependency] private readonly AtmosphereSystem _atmosphere = default!;
    [Dependency] private readonly TransformSystem _transform = default!;
    [Dependency] private readonly SharedContainerSystem _container = default!;
    [Dependency] private readonly SharedItemSystem _item = default!;
    [Dependency] private readonly ExplosionSystem _explosion = default!;
    [Dependency] private readonly SharedSingularitySystem _singularity = default!;
    [Dependency] private readonly SharedAppearanceSystem _appearance = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    [Dependency] private readonly SharedPhysicsSystem _physics = default!;

    private EntityQuery<PhysicsComponent> _physicsQuery = default!;

    public override void Initialize()
    {
        base.Initialize();
        _physicsQuery = GetEntityQuery<PhysicsComponent>();
        SubscribeLocalEvent<SupermatterProcessingComponent, AtmosDeviceUpdateEvent>(OnSupermatterAtmosUpdate);
        SubscribeLocalEvent<SupermatterProcessingComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<SupermatterProcessingComponent, EntityTerminatingEvent>(OnSupermatterTerminating);
        SubscribeLocalEvent<SupermatterStateComponent, DamageChangedEvent>(OnDamageChanged);
    }

    private void OnDamageChanged(Entity<SupermatterStateComponent> ent, ref DamageChangedEvent args)
    {
        if (!args.DamageIncreased || args.DamageDelta == null)
            return;

        var powerToAdd = (float)args.DamageDelta.GetTotal();
        // Radiation 10x multiplier - check for radiation damage type if/when we have it
        ent.Comp.Power += powerToAdd;
        Dirty(ent);
    }

    private void OnSupermatterAtmosUpdate(EntityUid uid, SupermatterProcessingComponent processing, ref AtmosDeviceUpdateEvent args)
    {
        if (!TryComp<SupermatterStateComponent>(uid, out var state))
            return;

        var dt = args.dt;
        var intervalSec = (float)processing.ProcessingInterval.TotalSeconds;
        var scale = intervalSec > 0 ? Math.Clamp(dt / intervalSec, 0.001f, 20f) : 1f;
        ProcessSupermatter(uid, state, processing, dt, scale);
    }

    private void OnMapInit(EntityUid uid, SupermatterProcessingComponent comp, MapInitEvent args)
    {
        // Ensure the supermatter's tile exists in GridAtmosphere so gas absorption can run.
        var xform = Transform(uid);
        if (xform.GridUid is { } gridUid && TryComp<GridAtmosphereComponent>(gridUid, out var gridAtmos))
        {
            var position = _transform.GetGridTilePositionOrDefault(uid);
            _atmosphere.InvalidateTile((gridUid, gridAtmos), position);
        }
        if (TryComp<SupermatterStateComponent>(uid, out var state))
        {
            UpdateSupermatterAppearance(uid, state);
            UpdateLoopingAmbientSound(uid, state, comp);
        }
    }

    private void OnSupermatterTerminating(EntityUid uid, SupermatterProcessingComponent comp, ref EntityTerminatingEvent args)
    {
        if (comp.AmbientLoopEntity is { } loopEnt && Exists(loopEnt))
            _audio.Stop(loopEnt);
    }

    /// <summary>
    /// Phase 9: Ash entities within range (similar to singularity ConsumeEntitiesInRange).
    /// Recursively ashes container contents first.
    /// </summary>
    private void AshEntitiesInRange(EntityUid uid, SupermatterStateComponent state, SupermatterProcessingComponent processing)
    {
        if (!TryComp<PhysicsComponent>(uid, out var body))
            return;

        foreach (var entity in _lookup.GetEntitiesInRange(uid, processing.AshingRange, flags: LookupFlags.Uncontained))
        {
            if (entity == uid)
                continue;

            if (_physicsQuery.TryComp(entity, out var otherBody) && !_physics.IsHardCollidable((uid, null, body), (entity, null, otherBody)))
                continue;

            AttemptAshEntity(uid, entity, state, processing);
        }
    }

    /// <summary>
    /// Attempts to ash an entity. Returns false if cancelled via SupermatterAttemptAshEntityEvent.
    /// </summary>
    private bool AttemptAshEntity(EntityUid supermatterUid, EntityUid target, SupermatterStateComponent state, SupermatterProcessingComponent processing)
    {
        if (!CanAshEntity(supermatterUid, target, processing))
            return false;

        AshEntity(supermatterUid, target, state, processing);
        return true;
    }

    /// <summary>
    /// Checks whether the supermatter can ash a given entity.
    /// </summary>
    private bool CanAshEntity(EntityUid supermatterUid, EntityUid target, SupermatterProcessingComponent processing)
    {
        var ev = new SupermatterAttemptAshEntityEvent(target, supermatterUid, processing);
        RaiseLocalEvent(target, ref ev);
        return !ev.Cancelled;
    }

    /// <summary>
    /// Ash an entity. Recursively ashes container contents first.
    /// </summary>
    private void AshEntity(EntityUid supermatterUid, EntityUid target, SupermatterStateComponent state, SupermatterProcessingComponent processing)
    {
        if (!Exists(target) || EntityManager.IsQueuedForDeletion(target))
            return;

        // Do not ash the ash we create - prevents infinite loop.
        if (MetaData(target).EntityPrototype?.ID == processing.AshingAshPrototype)
            return;

        // Recursively ash container contents first (entities inside target)
        if (TryComp<ContainerManagerComponent>(target, out var containerManager))
        {
            foreach (var container in _container.GetAllContainers(target, containerManager))
            {
                foreach (var contained in container.ContainedEntities.ToList())
                {
                    if (Exists(contained) && !EntityManager.IsQueuedForDeletion(contained))
                        AshEntity(supermatterUid, contained, state, processing);
                }
            }
        }

        var powerGained = 0f;
        var isLiving = HasComp<MobStateComponent>(target);

        if (TryComp<ItemComponent>(target, out var item))
        {
            var weight = _item.GetSizePrototype(item.Size).Weight;
            powerGained = weight switch
            {
                <= 2 => processing.AshingPowerSmall,
                <= 8 => processing.AshingPowerMedium,
                _ => processing.AshingPowerLarge
            };
            if (!isLiving)
                processing.MatterHealing += weight;
        }
        else if (TryComp<PhysicsComponent>(target, out var physics))
        {
            var mass = physics.Mass;
            powerGained = mass switch
            {
                < 15f => processing.AshingPowerSmall,
                < 80f => processing.AshingPowerMedium,
                _ => processing.AshingPowerLarge
            };
            if (!isLiving)
                processing.MatterHealing += MathF.Max(1f, mass / 10f);
        }
        else
        {
            powerGained = processing.AshingPowerBase;
            if (!isLiving)
                processing.MatterHealing += 1f;
        }

        state.Power += powerGained;
        if (isLiving)
            state.Integrity = MathF.Max(0f, state.Integrity - powerGained / processing.AshingLivingIntegrityDivisor);

        var coords = Transform(target).Coordinates;
        Spawn(processing.AshingAshPrototype, coords);
        _audio.PlayPvs(processing.AshingSound, supermatterUid);

        QueueDel(target);
        Dirty(supermatterUid, state);
    }

    /// <summary>
    /// Updates looping ambient: calm.ogg when normal, delamming.ogg when integrity &lt; 750.
    /// </summary>
    private void UpdateLoopingAmbientSound(EntityUid uid, SupermatterStateComponent state, SupermatterProcessingComponent processing)
    {
        var isDelamming = state.Integrity < 750f;
        if (processing.AmbientLoopIsDelamming == isDelamming && processing.AmbientLoopEntity.HasValue)
            return;

        var desiredSound = isDelamming ? processing.AmbientDelammingSound : processing.AmbientCalmSound;
        if (desiredSound == null)
            return;

        if (processing.AmbientLoopEntity is { } loopEnt && Exists(loopEnt))
        {
            _audio.Stop(loopEnt);
            processing.AmbientLoopEntity = null;
        }

        var result = _audio.PlayPvs(desiredSound, uid, AudioParams.Default.WithLoop(true));
        if (result.HasValue)
        {
            processing.AmbientLoopEntity = result.Value.Entity;
            processing.AmbientLoopIsDelamming = isDelamming;
        }
    }

    private void ProcessSupermatter(EntityUid uid, SupermatterStateComponent state, SupermatterProcessingComponent processing, float dt, float scale)
    {
        // Phase 9: Ash entities in range (range-based, like singularity)
        AshEntitiesInRange(uid, state, processing);

        // Phase 2: Gas absorption - center tile + 4 orthogonals, 9% per tile (no release yet)
        if (!AbsorbGas(uid, state, processing, scale))
        {
            Dirty(uid, state);
            return;
        }

        // Phase 3: Gas characteristics from absorbed mixture
        ComputeCharacteristics(state, processing);

        // Phase 10: Special gas interactions (Healium, Pluoxium, Nibbles)
        ProcessSpecialGasInteractions(uid, state, processing, scale);

        // Phase 6: Growth - modifies AbsorbedMixture before release
        ProcessGrowth(uid, state, processing, scale);
        ProcessReproduction(uid, state, processing, scale);

        // Release absorbed gas back to center tile (after Growth)
        ReleaseAbsorbedGas(uid, processing);

        // Phase 4 & 5: Power from Enthalpy (before temp shift), thermal delta, integrity damage
        var mix = processing.AbsorbedMixture;
        if (mix != null && mix.TotalMoles > 0 && state.Enthalpy != 0)
        {
            var t = mix.Temperature;
            state.Power += state.Enthalpy * (t - 293.15f) * scale;
        }

        // Phase 4: Power decay (scaled for tickrate invariance)
        state.Power *= MathF.Max(0f, 1f - processing.DecayStabilityMultiplier / 100f * state.Stability * scale);
        state.Power -= state.Stability * scale;
        state.Power = MathF.Max(0f, state.Power);

        // Phase 5: Apply thermal delta to center tile and integrity damage
        ApplyEnthalpyThermalDelta(uid, state, processing, scale);

        // Phase 8: Apply integrity changes (heal, damage, matter healing, cap)
        ApplyIntegrityChanges(uid, state, processing, scale);

        // Phase 11: Delamination when Integrity <= 0
        if (state.Integrity <= 0)
        {
            TriggerDelamination(uid, state, processing);
            return;
        }

        // Phase 7: Lightning (positive Conductivity)
        ProcessLightning(uid, state, processing, dt);

        UpdateLoopingAmbientSound(uid, state, processing);
        UpdateSupermatterAppearance(uid, state);
        Dirty(uid, state);
    }

    /// <summary>
    /// Phase 12: Update appearance based on integrity for client visuals.
    /// </summary>
    private void UpdateSupermatterAppearance(EntityUid uid, SupermatterStateComponent state)
    {
        var integrityMax = 1000f;
        var visualState = state.Integrity switch
        {
            >= 750f => (byte)0,
            >= 250f => (byte)1,
            >= 50f => (byte)2,
            _ => (byte)3
        };
        _appearance.SetData(uid, SupermatterVisualKeys.State, visualState);
    }

    /// <summary>
    /// Phase 11: Trigger delamination based on dominant characteristic.
    /// Growth -> Singularity; Conductivity -> Tesla; Enthalpy+/Stability- -> Explosion; else -> Resonance Cascade.
    /// </summary>
    private void TriggerDelamination(EntityUid uid, SupermatterStateComponent state, SupermatterProcessingComponent processing)
    {
        var coords = Transform(uid).Coordinates;
        var power = state.Power;

        var absGrowth = MathF.Abs(state.Growth);
        var absConductivity = MathF.Abs(state.Conductivity);
        var absEnthalpy = MathF.Abs(state.Enthalpy);
        var absStability = MathF.Abs(state.Stability - processing.StabilityDefault);

        var maxAbs = MathF.Max(MathF.Max(absGrowth, absConductivity), MathF.Max(absEnthalpy, absStability));

        if (maxAbs <= 0)
        {
            ResonanceCascade(uid, coords, power);
            return;
        }

        if (MathHelper.CloseTo(absGrowth, maxAbs))
        {
            SpawnSingularity(coords, power);
        }
        else if (MathHelper.CloseTo(absConductivity, maxAbs))
        {
            SpawnTesla(coords, power);
        }
        else if (MathHelper.CloseTo(absEnthalpy, maxAbs))
        {
            if (state.Enthalpy > 0)
                TriggerExplosion(uid, power);
            else
                ResonanceCascade(uid, coords, power);
        }
        else
        {
            if (state.Stability < 0)
                TriggerExplosion(uid, power);
            else
                ResonanceCascade(uid, coords, power);
        }

        QueueDel(uid);
    }

    private void SpawnSingularity(EntityCoordinates coords, float power)
    {
        var level = (byte)Math.Clamp((int)(power / 1000f), 1, SharedSingularitySystem.MaxSingularityLevel);
        var sing = Spawn("Singularity", coords);
        if (TryComp<SingularityComponent>(sing, out var singComp))
            _singularity.SetLevel(sing, level, singComp);
    }

    private void SpawnTesla(EntityCoordinates coords, float power)
    {
        var count = Math.Max(1, (int)(power / 100f));
        for (var i = 0; i < count; i++)
        {
            Spawn("TeslaMiniEnergyBall", coords);
        }
    }

    private void TriggerExplosion(EntityUid uid, float power)
    {
        var radius = MathF.Max(1f, power / 100f);
        _explosion.QueueExplosion(uid, ExplosionSystem.DefaultExplosionPrototypeId, radius * 10f, 2f, radius * 2f, user: uid, canCreateVacuum: true, addLog: true);
    }

    private void ResonanceCascade(EntityUid uid, EntityCoordinates coords, float power)
    {
        var radius = MathF.Max(0.5f, power / 500f);
        _explosion.QueueExplosion(uid, ExplosionSystem.DefaultExplosionPrototypeId, radius * 5f, 1.5f, radius, user: uid, canCreateVacuum: false, addLog: true);
    }

    /// <summary>
    /// Phase 7: Positive Conductivity lightning. Interval = max(0.5, 10 - 0.1*Conductivity) sec.
    /// Fire bolts per power thresholds (3000, 6000, 9000).
    /// </summary>
    private void ProcessLightning(EntityUid uid, SupermatterStateComponent state, SupermatterProcessingComponent processing, float dt)
    {
        if (state.Conductivity <= 0 || state.Power < processing.LightningPowerThresholds[0])
            return;

        if (processing.LightningTimer == TimeSpan.Zero)
            processing.LightningTimer = TimeSpan.FromSeconds(MathF.Max(processing.LightningIntervalMin,
                processing.LightningIntervalBase - processing.LightningIntervalConductivityFactor * state.Conductivity));

        processing.LightningTimer -= TimeSpan.FromSeconds(dt);
        if (processing.LightningTimer > TimeSpan.Zero)
            return;

        var interval = MathF.Max(processing.LightningIntervalMin,
            processing.LightningIntervalBase - processing.LightningIntervalConductivityFactor * state.Conductivity);
        processing.LightningTimer = TimeSpan.FromSeconds(interval);

        var boltCount = 0;
        foreach (var threshold in processing.LightningPowerThresholds)
        {
            if (state.Power >= threshold)
                boltCount++;
        }

        if (boltCount > 0 && processing.LightningSound != null)
            _audio.PlayPvs(processing.LightningSound, uid);

        for (var i = 0; i < boltCount; i++)
        {
            FireLightningBolt(uid, state);
            processing.LightningFiredCount++;
        }
    }

    /// <summary>
    /// Fires a lightning bolt from the supermatter. Placeholder - no visual/effect yet (Phase 12).
    /// </summary>
    private void FireLightningBolt(EntityUid uid, SupermatterStateComponent state)
    {
        // Placeholder: lightning bolt logic. Phase 12 adds visuals/audio.
        // For now we just track that we fired (LightningFiredCount in Processing).
    }

    /// <summary>
    /// Phase 5: Apply thermal delta (Enthalpy * 1 MJ) to mixture, update temperature, apply integrity damage.
    /// </summary>
    private void ApplyEnthalpyThermalDelta(EntityUid uid, SupermatterStateComponent state, SupermatterProcessingComponent processing, float scale)
    {
        if (state.Enthalpy == 0)
            return;

        var xform = Transform(uid);
        if (xform.GridUid is not { } gridUid || !TryComp<GridAtmosphereComponent>(gridUid, out var gridAtmos))
            return;

        var position = _transform.GetGridTilePositionOrDefault(uid);
        var centerMix = _atmosphere.GetTileMixture((gridUid, gridAtmos), null, position, true);
        if (centerMix == null || centerMix.Immutable || centerMix.TotalMoles <= 0)
            return;

        const float EnthalpyToEnergy = 1_000_000f; // 1 MJ per Enthalpy point
        var deltaE = state.Enthalpy * EnthalpyToEnergy * scale;

        var heatCap = _atmosphere.GetHeatCapacity(centerMix, true);
        if (heatCap < Atmospherics.MinimumHeatCapacity)
            return;

        var totalThermal = heatCap * centerMix.Temperature;
        totalThermal -= deltaE;
        centerMix.Temperature = totalThermal / heatCap;

        var integrityDamage = (centerMix.Temperature - 293.15f) / 100f * state.Enthalpy * scale;
        processing.IntegrityDamageThisTick += integrityDamage;
    }

    /// <summary>
    /// Phase 8: Apply all integrity changes (heal, damage, matter healing, cap).
    /// </summary>
    private void ApplyIntegrityChanges(EntityUid uid, SupermatterStateComponent state, SupermatterProcessingComponent processing, float scale)
    {
        var delta = 0f;

        if (state.Stability > 0)
            delta += state.Stability;

        delta -= state.Power / processing.PowerDamageDivisor;

        if (IsInVacuum(uid, processing))
            delta -= processing.VacuumDamage;

        delta -= processing.IntegrityDamageThisTick;
        processing.IntegrityDamageThisTick = 0;

        var matterHeals = MathF.Floor(processing.MatterHealing / processing.MatterHealingThreshold);
        if (matterHeals > 0)
        {
            delta += matterHeals;
            processing.MatterHealing -= matterHeals * processing.MatterHealingThreshold;
        }

        delta *= scale;
        delta = Math.Clamp(delta, -processing.DamageCap * scale, processing.DamageCap * scale);
        state.Integrity += delta;
        state.Integrity = Math.Clamp(state.Integrity, 0f, processing.IntegrityMax);
    }

    private bool IsInVacuum(EntityUid uid, SupermatterProcessingComponent processing)
    {
        var xform = Transform(uid);
        if (xform.GridUid is not { } gridUid || !TryComp<GridAtmosphereComponent>(gridUid, out var gridAtmos))
            return true;

        var position = _transform.GetGridTilePositionOrDefault(uid);
        var centerMix = _atmosphere.GetTileMixture((gridUid, gridAtmos), null, position, false);
        if (centerMix == null)
            return true;

        if (centerMix.Pressure < processing.VacuumThresholdKpa)
            return true;

        var enumerator = _atmosphere.GetAdjacentTileMixtures((gridUid, gridAtmos), position, false, false);
        while (enumerator.MoveNext(out var adjacent))
        {
            if (adjacent == null || adjacent.Pressure < processing.VacuumThresholdKpa)
                return true;
        }

        return false;
    }

    /// <summary>
    /// Absorbs gas from center tile and 4 orthogonals using scrubber pattern.
    /// Uses ratioPerTile (default 9%) per tile. Merges into AbsorbedMixture, processes, releases back.
    /// Returns false if absorption was skipped (no grid/atmos).
    /// </summary>
    private bool AbsorbGas(EntityUid uid, SupermatterStateComponent state, SupermatterProcessingComponent processing, float scale)
    {
        var xform = Transform(uid);
        if (xform.GridUid is not { } gridUid || !TryComp<GridAtmosphereComponent>(gridUid, out var gridAtmos))
            return false;

        var position = _transform.GetGridTilePositionOrDefault(uid);
        var ratio = MathF.Min(1f, processing.RatioPerTile * scale);

        // Center tile
        var centerMix = _atmosphere.GetTileMixture((gridUid, gridAtmos), null, position, true);
        if (centerMix == null || centerMix.Immutable)
            return false;

        // Create or clear absorbed mixture
        processing.AbsorbedMixture ??= new GasMixture(Atmospherics.CellVolume);
        processing.AbsorbedMixture.Clear();
        processing.AbsorbedMixture.Volume = Atmospherics.CellVolume;

        // Absorb from center
        var removed = centerMix.RemoveRatio(ratio);
        _atmosphere.Merge(processing.AbsorbedMixture, removed);

        // Absorb from 4 orthogonals
        var enumerator = _atmosphere.GetAdjacentTileMixtures((gridUid, gridAtmos), position, false, true);
        while (enumerator.MoveNext(out var adjacent))
        {
            if (adjacent == null || adjacent.Immutable)
                continue;
            removed = adjacent.RemoveRatio(ratio);
            _atmosphere.Merge(processing.AbsorbedMixture, removed);
        }

        return true;
    }

    /// <summary>
    /// Releases AbsorbedMixture back to the center tile. Call after ProcessGrowth.
    /// </summary>
    private void ReleaseAbsorbedGas(EntityUid uid, SupermatterProcessingComponent processing)
    {
        var xform = Transform(uid);
        if (xform.GridUid is not { } gridUid || !TryComp<GridAtmosphereComponent>(gridUid, out var gridAtmos))
            return;

        var position = _transform.GetGridTilePositionOrDefault(uid);
        var centerMix = _atmosphere.GetTileMixture((gridUid, gridAtmos), null, position, true);
        if (centerMix == null || centerMix.Immutable || processing.AbsorbedMixture == null)
            return;

        _atmosphere.Merge(centerMix, processing.AbsorbedMixture);
    }

    /// <summary>
    /// Phase 6: Negative Growth produces gas; Positive Growth absorbs gas.
    /// </summary>
    private void ProcessGrowth(EntityUid uid, SupermatterStateComponent state, SupermatterProcessingComponent processing, float scale)
    {
        var mix = processing.AbsorbedMixture;
        if (mix == null || mix.Immutable)
            return;

        if (state.Growth < 0)
        {
            var molsPerGas = MathF.Abs(state.Growth) * scale;
            if (float.IsInfinity(molsPerGas) || float.IsNaN(molsPerGas) || molsPerGas <= 0)
                return;
            molsPerGas = MathF.Min(molsPerGas, 1e6f);
            var n = (int)MathF.Floor((state.Power + processing.GrowthPowerThreshold) / processing.GrowthPowerThreshold);
            n = Math.Clamp(n, 1, SupermatterGasValues.NegativeGrowthProductionOrder.Length);

            var totalMols = 0f;
            for (var i = 0; i < n; i++)
            {
                var gas = SupermatterGasValues.NegativeGrowthProductionOrder[i];
                mix.AdjustMoles((int)gas, molsPerGas);
                totalMols += molsPerGas;
            }
            state.Power = MathF.Max(0f, state.Power - totalMols);
        }
        else if (state.Growth > 0 && mix.TotalMoles > 0)
        {
            var ratio = MathF.Min(1f, state.Growth / processing.GrowthAbsorptionDivisor * scale);
            if (float.IsInfinity(ratio) || float.IsNaN(ratio) || ratio <= 0)
                return;
            var removed = mix.RemoveRatio(ratio);
            var mols = removed.TotalMoles;
            state.Power += mols;
            processing.Reproduction += mols;
        }
    }

    /// <summary>
    /// Phase 6: Reproduction tick and shard spawning.
    /// </summary>
    private void ProcessReproduction(EntityUid uid, SupermatterStateComponent state, SupermatterProcessingComponent processing, float scale)
    {
        processing.SecondTally += processing.Reproduction * 0.1f * scale;
        processing.Reproduction *= (float)Math.Pow(processing.ReproductionDecay, scale);

        while (processing.SecondTally >= processing.ReproductionShardThreshold)
        {
            processing.SecondTally -= processing.ReproductionShardThreshold;
            var coords = Transform(uid).Coordinates;
            Spawn(processing.ShardPrototype, coords);
        }
    }

    /// <summary>
    /// Computes gas characteristics from AbsorbedMixture. Formula: C_raw = sum(mols[g]*value[g][C])/100.
    /// Stability = raw + 10. Growth/Conductivity/Enthalpy = raw * (1+Power/1000) * (10-Stability)/10.
    /// </summary>
    private void ComputeCharacteristics(SupermatterStateComponent state, SupermatterProcessingComponent processing)
    {
        var mix = processing.AbsorbedMixture;
        if (mix == null || mix.TotalMoles <= 0)
            return;

        float rawStability = 0, rawGrowth = 0, rawConductivity = 0, rawEnthalpy = 0;
        for (var i = 0; i < Atmospherics.AdjustedNumberOfGases; i++)
        {
            var mols = mix.GetMoles(i);
            if (mols <= 0)
                continue;
            var v = SupermatterGasValues.Get((Gas)i);
            rawStability += mols * v.Stability;
            rawGrowth += mols * v.Growth;
            rawConductivity += mols * v.Conductivity;
            rawEnthalpy += mols * v.Enthalpy;
        }

        var totalMols = mix.TotalMoles;
        rawStability /= 100f;
        rawGrowth /= 100f;
        rawConductivity /= 100f;
        rawEnthalpy /= 100f;

        state.Stability = rawStability + processing.StabilityDefault;

        var powerFactor = 1f + state.Power / processing.PowerMultiplier;
        var stabilityFactor = MathF.Max(0f, (processing.StabilityDefault - state.Stability) / processing.StabilityDefault);

        state.Growth = rawGrowth * powerFactor * stabilityFactor;
        state.Conductivity = rawConductivity * powerFactor * stabilityFactor;
        state.Enthalpy = rawEnthalpy * powerFactor * stabilityFactor;
    }

    /// <summary>
    /// Phase 10: Special gas interactions - Healium, Pluoxium, Nibbles (AntiNoblium + Helium).
    /// </summary>
    private void ProcessSpecialGasInteractions(EntityUid uid, SupermatterStateComponent state, SupermatterProcessingComponent processing, float scale)
    {
        var mix = processing.AbsorbedMixture;
        if (mix == null || mix.Immutable)
            return;

        // Healium: >= 10 mol Healium -> absorb 10 mol, heal 1 Integrity (scaled)
        var healiumMols = mix.GetMoles(Gas.Healium);
        var healAmount = processing.HealiumHealAmount * scale;
        if (healiumMols >= healAmount)
        {
            mix.AdjustMoles(Gas.Healium, -healAmount);
            state.Integrity = MathF.Min(processing.IntegrityMax, state.Integrity + processing.HealiumIntegrityPerHeal * scale);
        }

        // Pluoxium: >= 10 mol CO2 + O2, Enthalpy > 0 -> consume 10% of each, produce Pluoxium (scaled)
        if (state.Enthalpy > 0)
        {
            var co2 = mix.GetMoles(Gas.CarbonDioxide);
            var o2 = mix.GetMoles(Gas.Oxygen);
            var consumeRatio = processing.PluoxiumConsumeRatio * scale;
            if (co2 >= processing.PluoxiumMinMols && o2 >= processing.PluoxiumMinMols)
            {
                var consumeCo2 = co2 * consumeRatio;
                var consumeO2 = o2 * consumeRatio;
                var pluoxiumProduced = MathF.Min(consumeCo2, consumeO2);
                mix.AdjustMoles(Gas.CarbonDioxide, -consumeCo2);
                mix.AdjustMoles(Gas.Oxygen, -consumeO2);
                mix.AdjustMoles(Gas.Pluoxium, pluoxiumProduced);
            }
        }

        // Nibbles Anti-Lightning: >= 10 mol AntiNoblium + Helium, Conductivity > 0 -> consume 10% of each, 1 bolt/mol, convert AntiNob -> HyperNoblium (scaled)
        if (state.Conductivity > 0)
        {
            var antiNob = mix.GetMoles(Gas.AntiNoblium);
            var helium = mix.GetMoles(Gas.Helium);
            var nibblesConsumeRatio = processing.NibblesConsumeRatio * scale;
            if (antiNob >= processing.NibblesMinMols && helium >= processing.NibblesMinMols)
            {
                var consumeAntiNob = antiNob * nibblesConsumeRatio;
                var consumeHelium = helium * nibblesConsumeRatio;
                var molsConsumed = MathF.Min(consumeAntiNob, consumeHelium);
                mix.AdjustMoles(Gas.AntiNoblium, -consumeAntiNob);
                mix.AdjustMoles(Gas.Helium, -consumeHelium);
                mix.AdjustMoles(Gas.HyperNoblium, consumeAntiNob);

                var boltCount = (int)MathF.Floor(molsConsumed);
                if (boltCount > 0 && processing.AntiLightningSound != null)
                    _audio.PlayPvs(processing.AntiLightningSound, uid);
                for (var i = 0; i < boltCount; i++)
                {
                    FireLightningBolt(uid, state);
                    processing.LightningFiredCount++;
                }
            }
        }
    }
}
