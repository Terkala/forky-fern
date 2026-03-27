namespace Content.Server._Funkystation.MageAscension;

/// <summary>
/// Ley line linked to an anomaly containment vessel (Funky Station science harvest flow).
/// </summary>
[RegisterComponent]
public sealed partial class ConfluenceVesselLinkComponent : Component
{
    [ViewVariables]
    public EntityUid? ConnectedVessel;
}
