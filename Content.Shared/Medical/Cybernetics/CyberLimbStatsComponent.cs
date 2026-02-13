namespace Content.Shared.Medical.Cybernetics;

/// <summary>
/// Component that tracks aggregate stats across all cyber-limbs on an entity.
/// This component is added to entities with IntegrityComponent to track battery, service time, and efficiency.
/// Server-only (not networked) to avoid LastComponentRemoved triggering client crashes when removed from player bodies.
/// </summary>
[RegisterComponent]
public sealed partial class CyberLimbStatsComponent : Component
{
    /// <summary>
    /// Total battery capacity from all installed battery modules (in Joules).
    /// </summary>
    [DataField]
    public float BatteryCapacity;

    /// <summary>
    /// Current battery charge (in Joules).
    /// </summary>
    [DataField]
    public float CurrentBatteryCharge;

    /// <summary>
    /// Total service time remaining from all matter bin modules (in seconds).
    /// </summary>
    [DataField]
    public TimeSpan ServiceTimeRemaining;

    /// <summary>
    /// Efficiency percentage (base 100%, modified by manipulator modules).
    /// </summary>
    [DataField]
    public float Efficiency = 100f;

    /// <summary>
    /// Last update timestamp for delta calculations.
    /// </summary>
    [DataField]
    public TimeSpan LastUpdate;

    /// <summary>
    /// Count of special modules (tool, utility, bio-battery) installed across all cyber-limbs.
    /// </summary>
    [DataField]
    public int SpecialModuleCount;
}
