using Robust.Shared.GameStates;

namespace Content.Shared.Medical.Cybernetics;

/// <summary>
/// Component that tracks aggregate stats across all cyber-limbs on an entity.
/// This component is added to entities with IntegrityComponent to track battery, service time, and efficiency.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class CyberLimbStatsComponent : Component
{
    /// <summary>
    /// Total battery capacity from all installed battery modules (in Joules).
    /// </summary>
    [DataField, AutoNetworkedField]
    public float BatteryCapacity;

    /// <summary>
    /// Current battery charge (in Joules).
    /// </summary>
    [DataField, AutoNetworkedField]
    public float CurrentBatteryCharge;

    /// <summary>
    /// Total service time remaining from all matter bin modules (in seconds).
    /// </summary>
    [DataField, AutoNetworkedField]
    public TimeSpan ServiceTimeRemaining;

    /// <summary>
    /// Efficiency percentage (base 100%, modified by manipulator modules).
    /// </summary>
    [DataField, AutoNetworkedField]
    public float Efficiency = 100f;

    /// <summary>
    /// Last update timestamp for delta calculations.
    /// </summary>
    [DataField, AutoNetworkedField]
    public TimeSpan LastUpdate;
}
