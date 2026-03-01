using Robust.Shared.GameStates;

namespace Content.Shared.Cybernetics.Components;

/// <summary>
/// Caches cyber limb stats for a body. Service time is a shared pool (base per limb + matter bins) that drains when limbs are installed.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class CyberLimbStatsComponent : Component
{
    /// <summary>
    /// Remaining service time in the shared pool. Computed as BaseServiceRemaining + sum(matter bin ServiceRemaining).
    /// </summary>
    [DataField]
    public TimeSpan ServiceTimeRemaining { get; set; }

    /// <summary>
    /// Maximum service time when all limbs are freshly repaired. Computed as BaseServiceTimePerLimb * limbCount + sum(matter bin ServiceTime).
    /// </summary>
    [DataField]
    public TimeSpan ServiceTimeMax { get; set; }

    /// <summary>
    /// Efficiency multiplier. Limb efficiency from manipulators, multiplied by external modifiers (e.g. 0.5 when depleted).
    /// </summary>
    [DataField]
    public float Efficiency { get; set; } = 1f;

    /// <summary>
    /// Minimum service time per cyber limb. Limbs function without modules (poorly) with this base.
    /// </summary>
    [DataField]
    public TimeSpan BaseServiceTimePerLimb { get; set; } = TimeSpan.FromMinutes(5);

    /// <summary>
    /// Remaining base service time. 5 min per limb when limb installed; drains at 1 sec/sec.
    /// </summary>
    [DataField]
    public TimeSpan BaseServiceRemaining { get; set; }

    /// <summary>
    /// Sum of charge across all batteries in cyber limb storage. Computed by CyberLimbStatsSystem.
    /// </summary>
    [DataField]
    public float BatteryRemaining { get; set; }

    /// <summary>
    /// Sum of MaxCharge across all batteries. 0 when no batteries.
    /// </summary>
    [DataField]
    public float BatteryMax { get; set; }

    /// <summary>
    /// Base battery drain rate in joules per second (watts). PowerCellMedium 720 J / 20 min = 0.6 J/s.
    /// </summary>
    [DataField]
    public float BaseBatteryDrainPerSecond { get; set; } = 0.6f;
}
