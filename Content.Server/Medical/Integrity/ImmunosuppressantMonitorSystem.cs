using Content.Shared.Chemistry.Components;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.EntityEffects.Effects.Medical;
using Content.Shared.FixedPoint;
using Content.Shared.Medical.Integrity;
using Content.Shared.Prototypes;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Server.Medical.Integrity;

/// <summary>
/// Server-side system that monitors immunosuppressant reagent amounts in the bloodstream
/// and automatically adjusts temporary integrity as reagents are metabolized.
/// </summary>
public sealed class ImmunosuppressantMonitorSystem : EntitySystem
{
    [Dependency] private readonly SharedSolutionContainerSystem _solutionContainer = default!;
    [Dependency] private readonly SharedIntegritySystem _integritySystem = default!;
    [Dependency] private readonly IPrototypeManager _prototypeManager = default!;
    [Dependency] private readonly IGameTiming _gameTiming = default!;

    /// <summary>
    /// Update interval for monitoring reagent amounts (1 second).
    /// </summary>
    private static readonly TimeSpan UpdateInterval = TimeSpan.FromSeconds(1.0);

    private TimeSpan _nextUpdate = TimeSpan.Zero;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<ImmunosuppressantTrackerComponent, ComponentInit>(OnComponentInit);
    }

    private void OnComponentInit(Entity<ImmunosuppressantTrackerComponent> ent, ref ComponentInit args)
    {
        // Initialize tracking on component creation
        UpdateTracker(ent);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var currentTime = _gameTiming.CurTime;
        if (currentTime < _nextUpdate)
            return;

        _nextUpdate = currentTime + UpdateInterval;

        var query = EntityQueryEnumerator<ImmunosuppressantTrackerComponent, IntegrityComponent>();
        while (query.MoveNext(out var uid, out var tracker, out var integrity))
        {
            UpdateTracker((uid, tracker), integrity);
        }
    }

    private void UpdateTracker(Entity<ImmunosuppressantTrackerComponent> tracker, IntegrityComponent? integrity = null)
    {
        if (!Resolve(tracker, ref integrity, logMissing: false))
            return;

        // Get the bloodstream solution
        Entity<SolutionComponent>? solutionEntity = null;
        if (!_solutionContainer.TryGetSolution(tracker.Owner, tracker.Comp.BloodstreamSolutionName, out solutionEntity, out var solution))
        {
            // No solution found, remove all tracked immunosuppressants
            CleanupTracker(tracker, integrity);
            return;
        }

        var changed = false;
        var reagentsToRemove = new List<ProtoId<ReagentPrototype>>();

        // For each tracked reagent, check current amount in solution
        foreach (var (reagentId, trackedBonus) in tracker.Comp.ActiveImmunosuppressants)
        {
            // Query current reagent amount in solution
            var currentAmount = solution.GetTotalPrototypeQuantity(reagentId);

            // If amount is zero or solution doesn't exist, mark for removal
            if (currentAmount <= FixedPoint2.Zero)
            {
                reagentsToRemove.Add(reagentId);
                changed = true;
                continue;
            }

            // Get the effect prototype to find IntegrityPerUnit
            // We need to find the metabolism effect for this reagent
            if (!_prototypeManager.TryIndex(reagentId, out ReagentPrototype? reagentProto))
            {
                reagentsToRemove.Add(reagentId);
                changed = true;
                continue;
            }

            // Find the ImmunosuppressantMetabolism effect for this reagent
            float integrityPerUnit = 0f;
            bool foundEffect = false;

            if (reagentProto.Metabolisms != null)
            {
                foreach (var metabolismGroup in reagentProto.Metabolisms.Values)
                {
                    if (metabolismGroup.Effects == null)
                        continue;

                    foreach (var effect in metabolismGroup.Effects)
                    {
                        if (effect is ImmunosuppressantMetabolism immunoEffect && 
                            immunoEffect.ReagentId == reagentId)
                        {
                            integrityPerUnit = immunoEffect.IntegrityPerUnit;
                            foundEffect = true;
                            break;
                        }
                    }

                    if (foundEffect)
                        break;
                }
            }

            if (!foundEffect)
            {
                reagentsToRemove.Add(reagentId);
                changed = true;
                continue;
            }

            // Calculate expected bonus: currentAmount * IntegrityPerUnit
            var expectedBonus = currentAmount * FixedPoint2.New(integrityPerUnit);

            // If expected bonus differs from tracked bonus, update tracker
            if (expectedBonus != trackedBonus)
            {
                tracker.Comp.ActiveImmunosuppressants[reagentId] = expectedBonus;
                changed = true;
            }
        }

        // Remove reagents that are no longer present
        foreach (var reagentId in reagentsToRemove)
        {
            tracker.Comp.ActiveImmunosuppressants.Remove(reagentId);
        }

        // Recalculate TotalBonus
        var oldTotal = tracker.Comp.TotalBonus;
        tracker.Comp.TotalBonus = FixedPoint2.Zero;
        foreach (var (_, reagentBonus) in tracker.Comp.ActiveImmunosuppressants)
        {
            tracker.Comp.TotalBonus += reagentBonus;
        }

        // Sync with IntegrityComponent.TemporaryIntegrityBonus
        if (changed || tracker.Comp.TotalBonus != integrity.TemporaryIntegrityBonus)
        {
            var difference = tracker.Comp.TotalBonus - integrity.TemporaryIntegrityBonus;
            if (difference > FixedPoint2.Zero)
            {
                _integritySystem.AddTemporaryIntegrity(tracker, difference, integrity);
            }
            else if (difference < FixedPoint2.Zero)
            {
                _integritySystem.RemoveTemporaryIntegrity(tracker, -difference, integrity);
            }
        }

        // Remove tracker component if no active immunosuppressants remain
        if (tracker.Comp.ActiveImmunosuppressants.Count == 0)
        {
            RemComp<ImmunosuppressantTrackerComponent>(tracker);
            return;
        }

        // Bio-rejection is fully suppressed while immunosuppressants are active
        // Set CurrentBioRejection to zero and mark for update
        if (integrity.CurrentBioRejection != FixedPoint2.Zero)
        {
            integrity.CurrentBioRejection = FixedPoint2.Zero;
            integrity.NeedsUpdate = true;
            Dirty(tracker, integrity);
        }

        if (changed)
        {
            Dirty(tracker, tracker.Comp);
        }
    }

    private void CleanupTracker(Entity<ImmunosuppressantTrackerComponent> tracker, IntegrityComponent integrity)
    {
        // Remove all integrity bonuses
        if (tracker.Comp.TotalBonus > FixedPoint2.Zero)
        {
            _integritySystem.RemoveTemporaryIntegrity(tracker, tracker.Comp.TotalBonus, integrity);
        }

        // Remove the tracker component
        RemComp<ImmunosuppressantTrackerComponent>(tracker);
    }
}
