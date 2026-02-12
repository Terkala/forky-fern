using Content.Shared.Damage;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Systems;
using Content.Shared.FixedPoint;

namespace Content.Shared.Medical.Integrity;

/// <summary>
/// System that manages integrity calculations and max health reduction.
/// </summary>
public abstract class SharedIntegritySystem : EntitySystem
{
    [Dependency] protected readonly DamageableSystem Damageable = default!;

    /// <summary>
    /// Recalculates target bio-rejection based on integrity usage.
    /// Surgery penalties are added directly to bio-rejection (handled separately).
    /// </summary>
    public void RecalculateTargetBioRejection(EntityUid uid, IntegrityComponent? integrity = null)
    {
        if (!Resolve(uid, ref integrity, logMissing: false))
            return;

        // Calculate effective max integrity (base + temporary bonus from immunosuppressants)
        var effectiveMaxIntegrity = FixedPoint2.New(integrity.MaxIntegrity) + integrity.TemporaryIntegrityBonus;
        
        // Calculate over limit
        var overLimit = integrity.UsedIntegrity - effectiveMaxIntegrity;
        if (overLimit < 0)
            overLimit = FixedPoint2.Zero;

        // Base target bio-rejection = (used - effectiveMax) * bioRejectionPerPoint
        // Surgery penalties are added separately (see UpdateSurgeryPenalty)
        var baseTargetBioRejection = overLimit * integrity.BioRejectionPerPoint;
        
        // Get surgery penalty contribution (added directly to bio-rejection)
        var ev = new GetTotalSurgeryPenaltyEvent();
        RaiseLocalEvent(uid, ref ev);
        var surgeryPenalty = ev.TotalPenalty;
        
        // Target bio-rejection = base + surgery penalty
        integrity.TargetBioRejection = baseTargetBioRejection + surgeryPenalty;
        
        // Mark as needing update if target changed
        if (integrity.TargetBioRejection != integrity.CurrentBioRejection)
        {
            integrity.NeedsUpdate = true;
        }
        
        Dirty(uid, integrity);
    }

    /// <summary>
    /// Adds integrity usage and recalculates target bio-rejection.
    /// </summary>
    public void AddIntegrityUsage(EntityUid uid, FixedPoint2 amount, IntegrityComponent? integrity = null)
    {
        if (!Resolve(uid, ref integrity, logMissing: false))
            return;

        integrity.UsedIntegrity += amount;
        integrity.NeedsUpdate = true; // Mark for update
        Dirty(uid, integrity);
        
        var ev = new IntegrityUsageChangedEvent(uid);
        RaiseLocalEvent(uid, ref ev);
        RecalculateTargetBioRejection(uid, integrity);
    }

    /// <summary>
    /// Removes integrity usage and recalculates target bio-rejection.
    /// </summary>
    public void RemoveIntegrityUsage(EntityUid uid, FixedPoint2 amount, IntegrityComponent? integrity = null)
    {
        if (!Resolve(uid, ref integrity, logMissing: false))
            return;

        integrity.UsedIntegrity = FixedPoint2.Max(FixedPoint2.Zero, integrity.UsedIntegrity - amount);
        integrity.NeedsUpdate = true; // Mark for update
        Dirty(uid, integrity);
        
        var ev = new IntegrityUsageChangedEvent(uid);
        RaiseLocalEvent(uid, ref ev);
        RecalculateTargetBioRejection(uid, integrity);
    }

    /// <summary>
    /// Adds temporary integrity bonus from immunosuppressants.
    /// </summary>
    public void AddTemporaryIntegrity(EntityUid uid, FixedPoint2 amount, IntegrityComponent? integrity = null)
    {
        if (!Resolve(uid, ref integrity, logMissing: false))
            return;

        integrity.TemporaryIntegrityBonus += amount;
        integrity.NeedsUpdate = true; // Mark for update
        Dirty(uid, integrity);
        RecalculateTargetBioRejection(uid, integrity);
    }

    /// <summary>
    /// Removes temporary integrity bonus from immunosuppressants.
    /// </summary>
    public void RemoveTemporaryIntegrity(EntityUid uid, FixedPoint2 amount, IntegrityComponent? integrity = null)
    {
        if (!Resolve(uid, ref integrity, logMissing: false))
            return;

        integrity.TemporaryIntegrityBonus = FixedPoint2.Max(FixedPoint2.Zero, integrity.TemporaryIntegrityBonus - amount);
        integrity.NeedsUpdate = true; // Mark for update
        Dirty(uid, integrity);
        RecalculateTargetBioRejection(uid, integrity);
    }
}

/// <summary>
/// Event raised when integrity usage changes (added or removed).
/// </summary>
[ByRefEvent]
public record struct IntegrityUsageChangedEvent(EntityUid Uid);
