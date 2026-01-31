using Robust.Shared.GameStates;

namespace Content.Shared.Medical.Cybernetics.Modules;

/// <summary>
/// Marker component for capacitor modules that multiply service time.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class CapacitorModuleComponent : Component
{
    /// <summary>
    /// Service time multiplier bonus (as a multiplier, e.g., 0.10 = 10% bonus).
    /// </summary>
    [DataField]
    public float ServiceTimeMultiplier = 0.10f;
}
