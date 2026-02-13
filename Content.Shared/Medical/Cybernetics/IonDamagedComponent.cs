using Content.Shared.FixedPoint;

namespace Content.Shared.Medical.Cybernetics;

/// <summary>
/// Component that tracks ion storm damage on cyber-limbs.
/// Adds a permanent bio-rejection penalty until repaired.
/// Server-only to avoid LastComponentRemoved triggering client crashes when removed from body parts.
/// </summary>
[RegisterComponent]
public sealed partial class IonDamagedComponent : Component
{
    /// <summary>
    /// Permanent bio-rejection penalty from ion storm damage.
    /// Contributes to TargetBioRejection calculation.
    /// </summary>
    [DataField]
    public FixedPoint2 BioRejectionPenalty = FixedPoint2.Zero;
}
