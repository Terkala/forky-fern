using Content.Shared.Administration.Logs;
using Content.Shared.Body;
using Content.Shared.Body.Part;
using Content.Shared.Body.Systems;
using Content.Shared.Database;
using Content.Shared.FixedPoint;
using Content.Shared.Medical.Cybernetics;
using Content.Shared.Medical.Integrity;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Server.Medical.Cybernetics;

/// <summary>
/// System that handles ion storm damage to cyber-limbs.
/// Applies either immediate service time expiration or long-term bio-rejection penalty.
/// </summary>
public sealed class IonStormCyberLimbSystem : EntitySystem
{
    [Dependency] private readonly IGameTiming _gameTiming = default!;
    [Dependency] private readonly SharedIntegritySystem _integritySystem = default!;
    [Dependency] private readonly ISharedAdminLogManager _adminLogger = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly SharedBodyPartSystem _bodyPartSystem = default!;
    [Dependency] private readonly CyberLimbStatsSystem _cyberLimbStats = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<IonDamageCyberLimbsEvent>(OnIonDamageCyberLimbs);
    }

    /// <summary>
    /// Handles ion storm damage to cyber-limbs.
    /// 40% chance: immediate service time expiration
    /// 60% chance: long-term bio-rejection penalty (+8)
    /// </summary>
    private void OnIonDamageCyberLimbs(ref IonDamageCyberLimbsEvent args)
    {
        var body = args.Body;

        if (!TryComp<BodyComponent>(body, out var bodyComp))
            return;

        if (!TryComp<CyberLimbStatsComponent>(body, out var stats))
            return;

        // Roll for damage type: 40% immediate service expiration, 60% long-term penalty
        bool immediateExpiration = _random.Prob(0.4f);

        if (immediateExpiration)
        {
            // Immediate service time expiration
            stats.ServiceTimeRemaining = TimeSpan.Zero;
            Dirty(body, stats);

            // Recalculate stats to apply 50% efficiency penalty immediately
            // Use preserveExpiredServiceTime=true to prevent RecalculateStats from refilling service time
            _cyberLimbStats.RecalculateStats(body, preserveExpiredServiceTime: true);

            // Update service time expiration to trigger integrity recalculation
            if (TryComp<IntegrityComponent>(body, out var integrity))
            {
                integrity.NextServiceTimeExpirationTime = _gameTiming.CurTime;
                integrity.NextServiceTimeExpiration = null;
                _integritySystem.RecalculateTargetBioRejection(body, integrity);
                Dirty(body, integrity);
            }

            _adminLogger.Add(LogType.Action, LogImpact.Medium,
                $"Ion storm affected {ToPrettyString(body)}: immediate service time expiration");

            // TODO: Add localization message
            // Loc.GetString("ion-storm-cyberlimb-service-expired", ("entity", body));
        }
        else
        {
            // Long-term bio-rejection penalty
            // Add IonDamagedComponent to all cyber-limbs on the body
            bool anyDamaged = false;
            foreach (var (partId, _) in _bodyPartSystem.GetBodyChildren(body, bodyComp))
            {
                if (!HasComp<CyberLimbComponent>(partId))
                    continue;

                // Add or increment component with +8 bio-rejection penalty (stacks across storms)
                var ionDamage = EnsureComp<IonDamagedComponent>(partId);
                ionDamage.BioRejectionPenalty += FixedPoint2.New(8);
                Dirty(partId, ionDamage);
                anyDamaged = true;
            }

            if (anyDamaged)
            {
                // Trigger integrity recalculation to include ion damage penalties
                if (TryComp<IntegrityComponent>(body, out var integrity))
                {
                    _integritySystem.RecalculateTargetBioRejection(body, integrity);
                }

                _adminLogger.Add(LogType.Action, LogImpact.Medium,
                    $"Ion storm affected {ToPrettyString(body)}: long-term bio-rejection penalty (+8, stacks with existing damage)");

                // TODO: Add localization message
                // Loc.GetString("ion-storm-cyberlimb-damaged", ("entity", body));
            }
        }
    }
}
