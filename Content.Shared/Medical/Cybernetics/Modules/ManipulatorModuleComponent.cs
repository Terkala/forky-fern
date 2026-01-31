using Robust.Shared.GameStates;

namespace Content.Shared.Medical.Cybernetics.Modules;

/// <summary>
/// Marker component for manipulator modules that provide efficiency bonuses.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class ManipulatorModuleComponent : Component
{
    /// <summary>
    /// Efficiency bonus per manipulator beyond the first (as a multiplier, e.g., 0.10 = 10%).
    /// </summary>
    [DataField]
    public float EfficiencyBonus = 0.10f;
}
