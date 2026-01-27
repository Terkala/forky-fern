using Content.Shared.Body;
using Content.Shared.Body.Components;
using Content.Shared.Body.Part;
using Content.Shared.Body.Systems;
using Content.Shared.FixedPoint;
using Content.Shared.Medical.Integrity;
using Content.Shared.Medical.Surgery;

namespace Content.Server.Medical.Integrity;

/// <summary>
/// Server-side implementation of SharedIntegritySystem.
/// Handles integrity calculations and surgery penalty tracking.
/// </summary>
public sealed class IntegritySystem : SharedIntegritySystem
{
    [Dependency] private readonly SharedBodyPartSystem _bodyPartSystem = default!;

    /// <summary>
    /// Gets the total surgery penalty from all body parts (as bio-rejection damage).
    /// Iterates through all body parts and sums their CurrentPenalty values.
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
        }

        return totalPenalty;
    }
}
