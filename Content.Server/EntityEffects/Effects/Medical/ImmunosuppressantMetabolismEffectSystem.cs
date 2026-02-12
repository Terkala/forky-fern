using Content.Shared.EntityEffects;
using Content.Shared.EntityEffects.Effects.Medical;
using Content.Shared.FixedPoint;
using Content.Shared.Medical.Integrity;

namespace Content.Server.EntityEffects.Effects.Medical;

/// <summary>
/// Metabolism effect that tracks immunosuppressant reagents and updates integrity bonuses.
/// Server-only because it depends on SharedIntegritySystem (concrete implementation is server-side).
/// </summary>
/// <inheritdoc cref="EntityEffectSystem{T,TEffect}"/>
public sealed partial class ImmunosuppressantMetabolismEffectSystem : EntityEffectSystem<IntegrityComponent, ImmunosuppressantMetabolism>
{
    [Dependency] private readonly SharedIntegritySystem _integritySystem = default!;

    protected override void Effect(Entity<IntegrityComponent> entity, ref EntityEffectEvent<ImmunosuppressantMetabolism> args)
    {
        // Ensure tracker component exists
        var tracker = EnsureComp<ImmunosuppressantTrackerComponent>(entity);

        // Calculate integrity bonus: IntegrityPerUnit * scale (which is reagent amount)
        var bonus = FixedPoint2.New(args.Effect.IntegrityPerUnit) * FixedPoint2.New(args.Scale);

        // Get previous bonus for this reagent (if any)
        var previousBonus = tracker.ActiveImmunosuppressants.GetValueOrDefault(args.Effect.ReagentId, FixedPoint2.Zero);
        var previousTotal = tracker.TotalBonus;

        // Update tracker's ActiveImmunosuppressants dictionary
        tracker.ActiveImmunosuppressants[args.Effect.ReagentId] = bonus;

        // Recalculate TotalBonus by summing all active immunosuppressants
        tracker.TotalBonus = FixedPoint2.Zero;
        foreach (var (_, reagentBonus) in tracker.ActiveImmunosuppressants)
        {
            tracker.TotalBonus += reagentBonus;
        }

        // Calculate the difference in total bonus
        var bonusDifference = tracker.TotalBonus - previousTotal;

        // Update integrity system
        if (bonusDifference > FixedPoint2.Zero)
        {
            _integritySystem.AddTemporaryIntegrity(entity, bonusDifference, entity.Comp);
        }
        else if (bonusDifference < FixedPoint2.Zero)
        {
            _integritySystem.RemoveTemporaryIntegrity(entity, -bonusDifference, entity.Comp);
        }

        Dirty(entity, tracker);
    }
}
