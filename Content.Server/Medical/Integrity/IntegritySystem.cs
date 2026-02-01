using Content.Shared.Body;
using Content.Shared.Body.Components;
using Content.Shared.Body.Part;
using Content.Shared.Body.Systems;
using Content.Shared.Damage;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Prototypes;
using Content.Shared.FixedPoint;
using Content.Shared.Medical.Cybernetics;
using Content.Shared.Medical.Integrity;
using Content.Shared.Medical.Surgery;
using Content.Shared.Medical.Surgery.Components;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Server.Medical.Integrity;

/// <summary>
/// Server-side implementation of SharedIntegritySystem.
/// Handles integrity calculations and surgery penalty tracking.
/// </summary>
public sealed class IntegritySystem : SharedIntegritySystem
{
    [Dependency] private readonly SharedBodyPartSystem _bodyPartSystem = default!;
    [Dependency] private readonly IGameTiming _gameTiming = default!;
    [Dependency] private readonly IPrototypeManager _prototypeManager = default!;

    /// <summary>
    /// Update interval for bio-rejection adjustments.
    /// Controls how frequently bio-rejection adjusts (0.2 per tick means 2 per second at this interval).
    /// </summary>
    private static readonly TimeSpan UpdateInterval = TimeSpan.FromSeconds(0.1);

    /// <summary>
    /// Bio-rejection damage type prototype ID.
    /// </summary>
    private const string BioRejectionDamageType = "BioRejection";

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<IntegrityComponent, ComponentInit>(OnComponentInit);
        SubscribeLocalEvent<IntegrityComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<IntegrityComponent, EntityUnpausedEvent>(OnUnpaused);

        SubscribeLocalEvent<SurgeryPenaltyComponent, ComponentInit>(OnSurgeryPenaltyInit);
        SubscribeLocalEvent<SurgeryPenaltyComponent, MapInitEvent>(OnSurgeryPenaltyMapInit);
        SubscribeLocalEvent<SurgeryPenaltyComponent, EntityUnpausedEvent>(OnSurgeryPenaltyUnpaused);

        SubscribeLocalEvent<CyberLimbComponent, CyberLimbPanelChangedEvent>(OnCyberLimbPanelChanged);
    }

    private void OnComponentInit(Entity<IntegrityComponent> ent, ref ComponentInit args)
    {
        ent.Comp.NextUpdate = _gameTiming.CurTime + UpdateInterval;
        ent.Comp.NeedsUpdate = true;
    }

    private void OnMapInit(Entity<IntegrityComponent> ent, ref MapInitEvent args)
    {
        ent.Comp.NextUpdate = _gameTiming.CurTime + UpdateInterval;
        ent.Comp.NeedsUpdate = true;
    }

    private void OnUnpaused(Entity<IntegrityComponent> ent, ref EntityUnpausedEvent args)
    {
        ent.Comp.NextUpdate += args.PausedTime;
    }

    private void OnSurgeryPenaltyInit(Entity<SurgeryPenaltyComponent> ent, ref ComponentInit args)
    {
        ent.Comp.NextUpdate = _gameTiming.CurTime + UpdateInterval;
        ent.Comp.NeedsUpdate = true;
    }

    private void OnSurgeryPenaltyMapInit(Entity<SurgeryPenaltyComponent> ent, ref MapInitEvent args)
    {
        ent.Comp.NextUpdate = _gameTiming.CurTime + UpdateInterval;
        ent.Comp.NeedsUpdate = true;
    }

    private void OnSurgeryPenaltyUnpaused(Entity<SurgeryPenaltyComponent> ent, ref EntityUnpausedEvent args)
    {
        ent.Comp.NextUpdate += args.PausedTime;
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        UpdateBioRejection();
        UpdateSurgeryPenalties();
    }

    /// <summary>
    /// Updates bio-rejection damage for all entities with IntegrityComponent.
    /// Gradually adjusts CurrentBioRejection toward TargetBioRejection at 0.2 per tick.
    /// </summary>
    private void UpdateBioRejection()
    {
        var query = EntityQueryEnumerator<IntegrityComponent, DamageableComponent>();
        var curTime = _gameTiming.CurTime;

        while (query.MoveNext(out var uid, out var integrity, out var damageable))
        {
            // Skip if not needing update
            if (!integrity.NeedsUpdate)
                continue;

            // Skip if not ready for update
            if (curTime < integrity.NextUpdate)
                continue;

            // Update next update time
            integrity.NextUpdate += UpdateInterval;

            // Skip if already at target
            if (integrity.CurrentBioRejection == integrity.TargetBioRejection)
            {
                integrity.NeedsUpdate = false;
                Dirty(uid, integrity);
                continue;
            }

            // Calculate adjustment amount (clamped to 0.2 per tick)
            var delta = integrity.TargetBioRejection - integrity.CurrentBioRejection;
            var adjustment = FixedPoint2.Clamp(delta, FixedPoint2.New(-0.2), FixedPoint2.New(0.2));
            
            // Apply adjustment
            integrity.CurrentBioRejection += adjustment;

            // Apply bio-rejection as damage
            if (!_prototypeManager.TryIndex<DamageTypePrototype>(BioRejectionDamageType, out var bioRejectionType))
            {
                Log.Error($"BioRejection damage type prototype not found: {BioRejectionDamageType}");
                continue;
            }

            var damageSpec = new DamageSpecifier(bioRejectionType, integrity.CurrentBioRejection);
            Damageable.SetDamage((uid, damageable), damageSpec);

            // Update NeedsUpdate flag
            if (integrity.CurrentBioRejection == integrity.TargetBioRejection)
            {
                integrity.NeedsUpdate = false;
            }

            Dirty(uid, integrity);
        }
    }

    /// <summary>
    /// Updates surgery penalties for all body parts.
    /// Gradually adjusts CurrentPenalty toward TargetPenalty at 0.2 per tick.
    /// </summary>
    private void UpdateSurgeryPenalties()
    {
        var query = EntityQueryEnumerator<SurgeryPenaltyComponent, BodyPartComponent>();
        var curTime = _gameTiming.CurTime;

        while (query.MoveNext(out var uid, out var penalty, out var bodyPart))
        {
            // Skip if not needing update
            if (!penalty.NeedsUpdate)
                continue;

            // Skip if not ready for update
            if (curTime < penalty.NextUpdate)
                continue;

            // Update next update time
            penalty.NextUpdate += UpdateInterval;

            // Skip if already at target
            if (penalty.CurrentPenalty == penalty.TargetPenalty)
            {
                penalty.NeedsUpdate = false;
                Dirty(uid, penalty);
                continue;
            }

            // Calculate adjustment amount (clamped to 0.2 per tick)
            var delta = penalty.TargetPenalty - penalty.CurrentPenalty;
            var adjustment = FixedPoint2.Clamp(delta, FixedPoint2.New(-0.2), FixedPoint2.New(0.2));
            
            // Apply adjustment
            penalty.CurrentPenalty += adjustment;
            Dirty(uid, penalty);

            // Update NeedsUpdate flag
            if (penalty.CurrentPenalty == penalty.TargetPenalty)
            {
                penalty.NeedsUpdate = false;
            }

            // Trigger integrity recalculation on the body
            if (bodyPart.Body != null && TryComp<IntegrityComponent>(bodyPart.Body.Value, out var integrity))
            {
                RecalculateTargetBioRejection(bodyPart.Body.Value, integrity);
                integrity.NeedsUpdate = true;
                Dirty(bodyPart.Body.Value, integrity);
            }
        }
    }

    /// <summary>
    /// Gets the total surgery penalty from all body parts (as bio-rejection damage).
    /// Iterates through all body parts and sums their CurrentPenalty values.
    /// Also adds cyber-limb panel penalties: +1 for exposed panel, +2 total for open panel.
    /// </summary>
    protected override FixedPoint2 GetTotalSurgeryPenalty(EntityUid body)
    {
        if (!TryComp<BodyComponent>(body, out var bodyComp))
            return FixedPoint2.Zero;

        FixedPoint2 totalPenalty = FixedPoint2.Zero;

        // Iterate through all body parts and sum their surgery penalties
        foreach (var (partId, _) in _bodyPartSystem.GetBodyChildren(body, bodyComp))
        {
            if (TryComp<SurgeryPenaltyComponent>(partId, out var penalty))
            {
                totalPenalty += penalty.CurrentPenalty;
            }

            // Check for non-precision tool penalties (permanent)
            if (TryComp<NonPrecisionToolPenaltyComponent>(partId, out var nonPrecisionPenalty))
            {
                totalPenalty += nonPrecisionPenalty.PermanentPenalty;
            }

            // Check for cyber-limb panel penalties
            if (TryComp<CyberLimbComponent>(partId, out var cyberLimb))
            {
                // +1 penalty for exposed panel, +2 total for open panel
                if (cyberLimb.PanelOpen)
                {
                    totalPenalty += FixedPoint2.New(2);
                }
                else if (cyberLimb.PanelExposed)
                {
                    totalPenalty += FixedPoint2.New(1);
                }
            }
        }

        return totalPenalty;
    }

    /// <summary>
    /// Handles cyber-limb panel state changes and recalculates bio-rejection penalties.
    /// </summary>
    private void OnCyberLimbPanelChanged(Entity<CyberLimbComponent> ent, ref CyberLimbPanelChangedEvent args)
    {
        // Get the body this cyber-limb is attached to
        if (!TryComp<BodyPartComponent>(ent, out var bodyPart) || bodyPart.Body == null)
            return;

        // Recalculate bio-rejection for the body
        if (TryComp<IntegrityComponent>(bodyPart.Body.Value, out var integrity))
        {
            RecalculateTargetBioRejection(bodyPart.Body.Value, integrity);
        }
    }
}
