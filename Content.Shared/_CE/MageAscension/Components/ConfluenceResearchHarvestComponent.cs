using Robust.Shared.GameStates;

namespace Content.Shared._CE.MageAscension.Components;

/// <summary>
/// Research points remaining on an opened confluence (drained by science interaction).
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class ConfluenceResearchHarvestComponent : Component
{
    [DataField, AutoNetworkedField]
    public int PointsRemaining = 1000;
}
