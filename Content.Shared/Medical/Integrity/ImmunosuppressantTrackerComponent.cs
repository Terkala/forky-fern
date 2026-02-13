using Content.Shared.Chemistry.Reagent;
using Content.Shared.FixedPoint;
using Robust.Shared.Prototypes;

namespace Content.Shared.Medical.Integrity;

/// <summary>
/// Component that tracks active immunosuppressant reagents and their integrity bonuses.
/// Server-only to avoid LastComponentRemoved triggering client crashes when removed from player body.
/// </summary>
[RegisterComponent]
public sealed partial class ImmunosuppressantTrackerComponent : Component
{
    /// <summary>
    /// Maps reagent IDs to their current integrity bonus contribution.
    /// </summary>
    [DataField]
    public Dictionary<ProtoId<ReagentPrototype>, FixedPoint2> ActiveImmunosuppressants = new();

    /// <summary>
    /// Cached total bonus for quick access.
    /// </summary>
    [ViewVariables]
    public FixedPoint2 TotalBonus = FixedPoint2.Zero;

    /// <summary>
    /// Solution name to monitor (default "bloodstream").
    /// </summary>
    [DataField]
    public string BloodstreamSolutionName = "bloodstream";
}
