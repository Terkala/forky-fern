using Content.Shared.Chemistry.Reagent;
using Content.Shared.EntityEffects;
using Content.Shared.FixedPoint;
using Content.Shared.Medical.Integrity;
using Content.Shared.Prototypes;
using Robust.Shared.Prototypes;

namespace Content.Shared.EntityEffects.Effects.Medical;

/// <summary>
/// Metabolism effect that tracks immunosuppressant reagents and updates integrity bonuses.
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

/// <summary>
/// Effect data for immunosuppressant metabolism.
/// </summary>
/// <inheritdoc cref="EntityEffectBase{T}"/>
public sealed partial class ImmunosuppressantMetabolism : EntityEffectBase<ImmunosuppressantMetabolism>
{
    /// <summary>
    /// Integrity bonus per unit of reagent (e.g., 0.5 for basic, 1.0 for advanced).
    /// </summary>
    [DataField]
    public float IntegrityPerUnit = 0.5f;

    /// <summary>
    /// The reagent being tracked.
    /// </summary>
    [DataField(required: true)]
    public ProtoId<ReagentPrototype> ReagentId;

    public override string? EntityEffectGuidebookText(IPrototypeManager prototype, IEntitySystemManager entSys)
    {
        return Loc.GetString(
            "entity-effect-guidebook-immunosuppressant",
            ("bonus", IntegrityPerUnit));
    }

}
