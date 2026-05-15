using Robust.Shared.GameStates;

namespace Content.Shared._CE.MageAscension.Components;

/// <summary>
/// Tracks mage ascension progress on the mind so objectives stay valid after the body is replaced.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class MageAscensionMindTrackerComponent : Component
{
    [DataField, AutoNetworkedField]
    public int LeylinesOpenedCount;

    [DataField, AutoNetworkedField]
    public bool CompletedDimensionalRiftAscension;
}
