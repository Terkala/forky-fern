using Robust.Shared.GameStates;

namespace Content.Shared.Medical.Cybernetics.Modules;

/// <summary>
/// Marker component for matter bin modules that contribute to cyber-limb service time.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class MatterBinModuleComponent : Component
{
    /// <summary>
    /// Service time contribution in seconds.
    /// </summary>
    [DataField]
    public float ServiceTimeContribution = 600f; // 10 minutes
}
