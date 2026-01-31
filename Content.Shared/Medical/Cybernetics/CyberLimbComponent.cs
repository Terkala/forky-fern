using Content.Shared.Storage;
using Content.Shared.Wires;
using Robust.Shared.Containers;
using Robust.Shared.GameStates;

namespace Content.Shared.Medical.Cybernetics;

/// <summary>
/// Component for cybernetic limbs that can store modules and require maintenance panels for access.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class CyberLimbComponent : Component
{
    /// <summary>
    /// Storage container reference for modules (2×3 grid via StorageComponent).
    /// This references the StorageComponent's container, which uses StorageComponent.ContainerId.
    /// </summary>
    [ViewVariables]
    public Container? StorageContainer;

    /// <summary>
    /// Next service time for this cyber-limb.
    /// </summary>
    [DataField, AutoNetworkedField]
    public TimeSpan NextServiceTime;

    /// <summary>
    /// Whether the maintenance panel is exposed (triggers +1 bio-rejection penalty).
    /// </summary>
    [DataField, AutoNetworkedField]
    public bool PanelExposed;

    /// <summary>
    /// Whether the maintenance panel is open (triggers +2 total bio-rejection penalty).
    /// </summary>
    [DataField, AutoNetworkedField]
    public bool PanelOpen;
}
