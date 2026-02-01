using Content.Shared.FixedPoint;
using Robust.Shared.GameStates;

namespace Content.Shared.Medical.Cybernetics;

/// <summary>
/// Component that tracks ion storm damage on cyber-limbs.
/// Adds a permanent bio-rejection penalty until repaired.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class IonDamagedComponent : Component
{
    /// <summary>
    /// Permanent bio-rejection penalty from ion storm damage.
    /// Contributes to TargetBioRejection calculation.
    /// </summary>
    [DataField, AutoNetworkedField]
    public FixedPoint2 BioRejectionPenalty = FixedPoint2.Zero;
}
