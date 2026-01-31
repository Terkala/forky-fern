using Robust.Shared.GameStates;

namespace Content.Shared.Medical.Cybernetics.Modules;

/// <summary>
/// Marker component for battery modules that contribute to cyber-limb battery capacity.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class BatteryModuleComponent : Component
{
    /// <summary>
    /// Battery capacity contribution in Joules.
    /// </summary>
    [DataField]
    public float CapacityContribution = 10000f;
}
